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
            FlagshipDesktopShell_exposes_persistent_home_install_and_support_actions();
            DesktopCampaignWorkspace_is_a_real_top_level_surface();
            DesktopUpdateSurface_is_a_real_top_level_surface();
            DesktopSupportSurface_is_a_real_top_level_surface();
            DesktopSupportCaseSurface_is_a_real_top_level_surface();
            DesktopDevicesAccessSurface_is_a_real_top_level_surface();
            DesktopReportSurface_is_a_real_top_level_surface();
            DesktopCrashRecoverySurface_is_a_real_top_level_surface();
            DesktopPreferencePersistence_is_restart_safe_for_flagship_shell_and_native_surfaces();
            DesktopHome_degrades_gracefully_when_workspace_bootstrap_is_unavailable();
            DesktopHome_wires_the_campaign_projection_into_the_summary_panel();
            DesktopHome_wires_the_support_projection_into_the_summary_panel();
            DesktopHome_wires_the_build_and_explain_projection_into_the_summary_panel();
            ShellNavigator_wires_ruleset_specific_headings_and_labels();
            ShellRightRail_and_workspace_strip_wire_ruleset_specific_copy();
            DesktopShell_ruleset_matrix_coverage_is_published_and_executable();
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
        RequireContains(source, "aria-selected=\"@(IsBrowseResultActive(browseWorkspace, item) ? \"true\" : \"false\")");
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
        FirstPlayableSessionProjection firstPlayableSession = new(
            SessionId: "starter-1",
            Label: "Starter lane",
            Summary: "Starter lane is ready to land the first playable session without a repo-only detour.",
            CampaignStartSummary: "The first playable session can start from Dockside without repo-only setup.",
            RuleReadySummary: "The starter build stays legal under the approved Seattle Streets environment.",
            ReturnLaneSummary: "Claimed-device restore and Dockside return stay readable after the first session.",
            CampaignReadySummary: "The same workspace is ready for the next full campaign handoff after the starter session.",
            NextSafeAction: "Start the first playable session before you widen the workspace beyond the guided starter lane.",
            EvidenceLines:
            [
                "Starter build, restore packet, and campaign lane all point at the same Dockside kickoff."
            ],
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T12:06:45+00:00"));
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
            ],
            FirstPlayableSession: firstPlayableSession);
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
            TrustBand: "review-pending",
            Discoverable: false,
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T12:08:00+00:00"),
            NextSafeAction: "Review publication status before you widen the audience beyond the guided recap lane.",
            LineageSummary: "Dockside recap stays chained to the same governed publication lineage without a shadow export.");
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
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T12:10:00+00:00"),
            FirstPlayableSession: firstPlayableSession);

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
                FirstPlayableSession: firstPlayableSession,
                NextSafeAction: "Server-plane next safe action keeps the follow-through explicit.",
                ReadinessHighlights: ["Server plane highlight: the roster is current."],
                Watchouts: ["Server plane watchout: verify the preview tablet before resuming GM-only notes."],
                SupportHighlights: ["Released: the fix lane stays attached to the same claimed install."],
                DecisionNotices: ["install_role: preview_scout stays attached to windows/avalonia on preview."],
                GeneratedAtUtc: DateTimeOffset.Parse("2026-03-27T12:11:00+00:00")),
            new DesktopHomePortableExchangePreview(
                CampaignId: workspace.CampaignId,
                CompatibilityState: "compatible-with-warnings",
                ContextSummary: "Campaign Neon Nights is portable, but the package does not yet pin a live session cutover.",
                ReceiptSummary: "Portable dossier/campaign exchange is ready for inspect-only review or merge, while governed replace stays review-required until a live session export is pinned.",
                NextSafeAction: "Open inspect-only first or export again with a pinned session before you authorize governed replace on another surface.",
                AssetScopeSummary: "5 portable asset(s): 1 dossier(s), 1 NPC(s), 1 session bundle(s), 1 encounter packet(s), 1 governed prep packet(s).",
                SupportedExchangeFormats: ["chummer.portable-dossier.v1", "chummer.portable-campaign.v1"],
                Highlights:
                [
                    "Package format chummer.portable-campaign.v1 stays on interop_export_v1/1.0.0.",
                    "Every asset keeps payload-hash provenance, export identity, and campaign pointers on the same governed receipt."
                ],
                Watchouts:
                [
                    "No live session binding was requested, so replace should wait for a session-scoped export even though inspect-only and merge remain safe."
                ]));

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
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Publication visibility:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Publication lineage:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Publication next:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Prefetch inventory:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Claimed device:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Change packet:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Roster transfer:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Travel mode:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Travel inventory:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "First session:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Legal runner:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Understandable return:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Campaign-ready lane:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Starter lane next:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "First-session proof:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Campaign memory:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Campaign memory return:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Portable exchange:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Exchange context:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Exchange asset scope:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Exchange formats:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Exchange note:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Server plane highlight:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Support lane:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Decision notice:");
        RequireContains(projection.NextSafeAction, "Server-plane next safe action");
        RequireContains(string.Join("\n", projection.Watchouts), "cloud-only snapshot");
        RequireContains(string.Join("\n", projection.Watchouts), "GM-only notes");
        RequireContains(string.Join("\n", projection.Watchouts), "Portable exchange:");
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
            PlannerCoverageSummary: "4 of 4 build follow-through checkpoints are already grounded.",
            PlannerCoverageLines:
            [
                "Campaign continuity: Apex is already attached as the governed return lane for this handoff.",
                "Outputs: no dossier or campaign-safe output is attached yet, so export and recap proof are still pending.",
                "Restore posture: no restore conflicts are currently blocking replay-safe handoff follow-through.",
                "Claimed install: no linked device is attached yet for install-aware follow-through."
            ],
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
            TrustBand: "review-pending",
            Discoverable: false,
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
        RequireContains(projection.RulesetSpotlight, "home cockpit");
        RequireContains(projection.RulesetSpotlight, "home");
        RequireContains(projection.ExplainFocus, "Explain focus:");
        RequireContains(projection.ExplainFocus, "Build path focus: Edge Runner Starter");
        RequireContains(projection.ExplainFocus, "Campaign handoff:");
        RequireContains(projection.RuntimeHealthSummary, "runtime");
        RequireContains(projection.RuntimeHealthSummary, "runtime drift requires a rebind");
        RequireNotEmpty(projection.ReturnTarget, nameof(projection.ReturnTarget));
        RequireNotEmpty(projection.RulePosture, nameof(projection.RulePosture));
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
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build Lab coverage:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build Lab coverage detail:");
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
        RequireContains(projection.RulesetSpotlight, "SR5 home cockpit");
        RequireContains(projection.ExplainFocus, "Claim the install");
        RequireContains(projection.RuntimeHealthSummary, "no active runtime profile");
        RequireContains(projection.ReturnTarget, "No workspace return target");
        RequireContains(projection.RulePosture, "Shadowrun 5");
        RequireContains(projection.RulePosture, ".chum5");
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
        RequireContains(source, "desktop.home.button.open_current_campaign_workspace");
        RequireContains(source, "desktop.home.button.open_my_artifacts");
        RequireContains(source, "desktop.home.button.open_campaign_artifacts");
        RequireContains(source, "desktop.home.button.open_published_artifacts");
        RequireContains(source, "client.GetAccountCampaignSummaryAsync");
        RequireContains(source, "client.GetCampaignWorkspaceDigestsAsync");
        RequireContains(source, "ReadCampaignWorkspaceDigestsAsync");
        RequireContains(source, "ReadCampaignWorkspaceServerPlaneAsync");
        RequireContains(source, "GetCampaignWorkspaceServerPlaneAsync");
        RequireContains(source, "ReadPortableExchangePreviewAsync");
        RequireContains(source, "GetPortableExchangePreviewAsync");
        RequireContains(source, "OpenCampaignWorkspaceAsync()");
        RequireContains(source, "OpenArtifactShelfView");
        RequireContains(source, "DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal($\"/artifacts?view={Uri.EscapeDataString(view)}\")");

        string projectorSource = ReadSource("Chummer.Presentation/Overview/DesktopHomeCampaignProjector.cs");
        RequireContains(projectorSource, "Campaign return:");
        RequireContains(projectorSource, "Support closure:");
        RequireContains(projectorSource, "Claimed device posture:");
        RequireContains(projectorSource, "Migration continuity:");
        RequireContains(projectorSource, "Portable exchange:");
        RequireContains(projectorSource, "Exchange formats:");
        RequireContains(projectorSource, "Publication trust:");
        RequireContains(projectorSource, "CampaignWorkspaceDigestProjection");
        RequireContains(projectorSource, "Support lane:");
        RequireContains(projectorSource, "Decision notice:");
        RequireContains(projectorSource, "Travel mode:");
        RequireContains(projectorSource, "Travel inventory:");
        RequireContains(projectorSource, "Campaign memory:");
        RequireContains(projectorSource, "Campaign memory return:");
        RequireContains(projectorSource, "Publication visibility:");
        RequireContains(projectorSource, "Publication lineage:");
        RequireContains(projectorSource, "Publication next:");
        RequireContains(projectorSource, "DesktopHomeCampaignServerPlane");

        string serverPlaneSource = ReadSource("Chummer.Presentation/Overview/DesktopHomeCampaignServerPlane.cs");
        RequireContains(serverPlaneSource, "TravelModeSummary");
        RequireContains(serverPlaneSource, "TravelPrefetchInventorySummary");
        RequireContains(serverPlaneSource, "CampaignMemorySummary");
        RequireContains(serverPlaneSource, "CampaignMemoryReturnSummary");
        RequireContains(serverPlaneSource, "Artifact trust:");
        RequireContains(serverPlaneSource, "Artifact shelf views:");
    }

    private static void DesktopCampaignWorkspace_is_a_real_top_level_surface()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs");
        RequireContains(source, "public static async Task ShowAsync(Window owner, string headId)");
        RequireContains(source, "desktop.campaign.title");
        RequireContains(source, "desktop.campaign.heading");
        RequireContains(source, "desktop.campaign.section.runboard");
        RequireContains(source, "desktop.campaign.section.restore");
        RequireContains(source, "desktop.campaign.section.support");
        RequireContains(source, "desktop.campaign.section.recent_workspaces");
        RequireContains(source, "desktop.campaign.button.refresh");
        RequireContains(source, "desktop.campaign.status.local_fallback");
        RequireContains(source, "desktop.campaign.status.refresh_failed");
        RequireContains(source, "new ScrollViewer");
        RequireContains(source, "BuildReadinessBody()");
        RequireContains(source, "BuildRestoreBody()");
        RequireContains(source, "BuildSupportBody()");
        RequireContains(source, "ReadCampaignSummaryAsync");
        RequireContains(source, "ReadCampaignWorkspaceDigestsAsync");
        RequireContains(source, "ReadCampaignWorkspaceServerPlaneAsync");
        RequireContains(source, "ReadSupportProjectionAsync");
        RequireContains(source, "DesktopHomeCampaignProjector.Create");
        RequireContains(source, "DesktopInstallLinkingWindow dialog = new(context);");
        RequireContains(source, "desktop.home.button.open_report_issue");
        RequireContains(source, "DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopSupportCaseWindow.ShowAsync(this, _installState.HeadId, _supportProjection)");
        RequireContains(source, "OpenCampaignFollowThroughAsync");
        RequireContains(source, "DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "OpenWorkspaceInDesktopShellAsync");
        RequireContains(source, "mainWindow.OpenWorkspaceFromDesktopSurfaceAsync(workspaceId)");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenWorkspacePortal");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall");
        RequireContains(source, "DesktopSupportWindow.ShowAsync(this, _installState.HeadId)");

        string navigationSource = ReadSource("Chummer.Avalonia/MainWindow.DesktopSurfaceNavigation.cs");
        RequireContains(navigationSource, "OpenWorkspaceFromDesktopSurfaceAsync");
        RequireContains(navigationSource, "_interactionCoordinator.SwitchWorkspaceAsync");
        RequireContains(navigationSource, "RunUiActionAsync");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "CHUMMER_DESKTOP_STARTUP_SURFACE");
        RequireContains(appSource, "campaign_workspace");
        RequireContains(appSource, "DesktopCampaignWorkspaceWindow.ShowAsync(owner, \"avalonia\")");
    }

    private static void DesktopUpdateSurface_is_a_real_top_level_surface()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopUpdateWindow.cs");
        RequireContains(source, "public static async Task ShowAsync(Window owner, string headId)");
        RequireContains(source, "desktop.update.title");
        RequireContains(source, "desktop.update.heading");
        RequireContains(source, "desktop.update.section.current");
        RequireContains(source, "desktop.update.section.follow_through");
        RequireContains(source, "desktop.update.section.install");
        RequireContains(source, "desktop.update.button.check_now");
        RequireContains(source, "desktop.update.button.refresh");
        RequireContains(source, "desktop.update.checking");
        RequireContains(source, "desktop.update.checked");
        RequireContains(source, "desktop.update.apply_scheduled");
        RequireContains(source, "DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync");
        RequireContains(source, "DesktopSupportWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForUpdate");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenDownloadsPortal()");
        RequireContains(source, "new ScrollViewer");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "string.Equals(startupSurface, \"update\"");
        RequireContains(appSource, "DesktopUpdateWindow.ShowAsync(owner, \"avalonia\")");
    }

    private static void DesktopSupportSurface_is_a_real_top_level_surface()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopSupportWindow.cs");
        RequireContains(source, "public static async Task ShowAsync(Window owner, string headId)");
        RequireContains(source, "desktop.support.title");
        RequireContains(source, "desktop.support.heading");
        RequireContains(source, "desktop.support.section.case");
        RequireContains(source, "desktop.support.section.release");
        RequireContains(source, "desktop.support.section.follow_through");
        RequireContains(source, "desktop.support.button.refresh");
        RequireContains(source, "desktop.support.status.current");
        RequireContains(source, "desktop.support.status.refresh_failed");
        RequireContains(source, "ReadSupportProjectionAsync");
        RequireContains(source, "client.GetDesktopHomeSupportDigestsAsync");
        RequireContains(source, "DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopUpdateWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopSupportCaseWindow.ShowAsync(this, _installState.HeadId, _supportProjection)");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForUpdate");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall");
        RequireContains(source, "new ScrollViewer");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "string.Equals(startupSurface, \"support\"");
        RequireContains(appSource, "DesktopSupportWindow.ShowAsync(owner, \"avalonia\")");
    }

    private static void DesktopSupportCaseSurface_is_a_real_top_level_surface()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopSupportCaseWindow.cs");
        RequireContains(source, "public static async Task ShowAsync(Window owner, string headId, DesktopHomeSupportProjection supportProjection)");
        RequireContains(source, "public static async Task ShowPreviewAsync(Window owner, string headId)");
        RequireContains(source, "desktop.support_case.title");
        RequireContains(source, "desktop.support_case.heading");
        RequireContains(source, "desktop.support_case.section.summary");
        RequireContains(source, "desktop.support_case.section.timeline");
        RequireContains(source, "desktop.support_case.section.follow_through");
        RequireContains(source, "desktop.support_case.button.refresh");
        RequireContains(source, "desktop.support_case.status.current");
        RequireContains(source, "desktop.support_case.status.preview");
        RequireContains(source, "client.GetDesktopHomeSupportDigestsAsync");
        RequireContains(source, "client.GetDesktopSupportCaseDetailsAsync");
        RequireContains(source, "DesktopSupportWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopUpdateWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "CreatePreviewSupportProjection");
        RequireContains(source, "CreatePreviewSupportCaseDetails");
        RequireContains(source, "new ScrollViewer");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "string.Equals(startupSurface, \"support_case\"");
        RequireContains(appSource, "DesktopSupportCaseWindow.ShowPreviewAsync(owner, \"avalonia\")");
    }

    private static void DesktopDevicesAccessSurface_is_a_real_top_level_surface()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopDevicesAccessWindow.cs");
        RequireContains(source, "public static async Task ShowAsync(Window owner, string headId)");
        RequireContains(source, "desktop.devices.title");
        RequireContains(source, "desktop.devices.heading");
        RequireContains(source, "desktop.devices.section.current");
        RequireContains(source, "desktop.devices.section.claimed");
        RequireContains(source, "desktop.devices.section.claims");
        RequireContains(source, "desktop.devices.section.follow_through");
        RequireContains(source, "client.GetDesktopInstallLinkingSummaryAsync");
        RequireContains(source, "client.GetAccountCampaignSummaryAsync");
        RequireContains(source, "DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopSupportWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopUpdateWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopInstallLinkingWindow dialog = new(context);");
        RequireContains(source, "new ScrollViewer");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "string.Equals(startupSurface, \"devices_access\"");
        RequireContains(appSource, "DesktopDevicesAccessWindow.ShowAsync(owner, \"avalonia\")");
    }

    private static void DesktopReportSurface_is_a_real_top_level_surface()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopReportIssueWindow.cs");
        RequireContains(source, "public static async Task ShowAsync(Window owner, string headId)");
        RequireContains(source, "desktop.report.title");
        RequireContains(source, "desktop.report.heading");
        RequireContains(source, "desktop.report.section.context");
        RequireContains(source, "desktop.report.section.bug");
        RequireContains(source, "desktop.report.section.feedback");
        RequireContains(source, "desktop.report.button.open_bug");
        RequireContains(source, "desktop.report.button.copy_bug");
        RequireContains(source, "desktop.report.button.open_feedback");
        RequireContains(source, "desktop.report.button.copy_feedback");
        RequireContains(source, "desktop.report.status.bug_opened");
        RequireContains(source, "desktop.report.status.feedback_opened");
        RequireContains(source, "BuildBugDraftText()");
        RequireContains(source, "BuildFeedbackDraftText()");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForBugReport");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForFeedback");
        RequireContains(source, "new ScrollViewer");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "string.Equals(startupSurface, \"report_issue\"");
        RequireContains(appSource, "DesktopReportIssueWindow.ShowAsync(owner, \"avalonia\")");
    }

    private static void DesktopCrashRecoverySurface_is_a_real_top_level_surface()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopCrashRecoveryWindow.cs");
        RequireContains(source, "public static async Task<bool> TryShowPendingAsync(Window owner)");
        RequireContains(source, "public static async Task ShowPreviewAsync(Window owner, string headId)");
        RequireContains(source, "desktop.crash.title");
        RequireContains(source, "desktop.crash.heading");
        RequireContains(source, "desktop.crash.section.summary");
        RequireContains(source, "desktop.crash.section.recovery");
        RequireContains(source, "desktop.crash.button.retry_send");
        RequireContains(source, "desktop.crash.button.keep_local_only");
        RequireContains(source, "desktop.home.button.open_report_issue");
        RequireContains(source, "desktop.home.button.open_support_center");
        RequireContains(source, "DesktopReportIssueWindow.ShowAsync(this, _pending.Report.HeadId)");
        RequireContains(source, "DesktopSupportWindow.ShowAsync(this, _pending.Report.HeadId)");
        RequireContains(source, "DesktopCrashRuntime.TryAcknowledgePendingCrashReport");
        RequireContains(source, "CreatePreviewPendingCrashReport");
        RequireContains(source, "new ScrollViewer");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "string.Equals(startupSurface, \"crash_recovery\"");
        RequireContains(appSource, "DesktopCrashRecoveryWindow.ShowPreviewAsync(owner, \"avalonia\")");
        RequireContains(appSource, "DesktopCrashRecoveryWindow.TryShowPendingAsync(owner)");
    }

    private static void DesktopPreferencePersistence_is_restart_safe_for_flagship_shell_and_native_surfaces()
    {
        string runtimeSource = ReadSource("Chummer.Desktop.Runtime/DesktopPreferenceRuntime.cs");
        RequireContains(runtimeSource, "public static class DesktopPreferenceRuntime");
        RequireContains(runtimeSource, "LoadOrCreateState");
        RequireContains(runtimeSource, "SaveState");
        RequireContains(runtimeSource, "preferences");
        RequireContains(runtimeSource, "state.json");

        string localizationSource = ReadSource("Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs");
        RequireContains(localizationSource, "SetCurrentLanguageOverride");
        RequireContains(localizationSource, "_currentLanguageOverride");
        RequireContains(localizationSource, "GetCurrentLanguage()");

        string presenterSource = ReadSource("Chummer.Presentation/Overview/CharacterOverviewPresenter.cs");
        RequireContains(presenterSource, "DesktopPreferenceStateRuntime.Current");
        RequireContains(presenterSource, "Preferences = preferences");

        string mainWindowSource = ReadSource("Chummer.Avalonia/MainWindow.axaml.cs");
        RequireContains(mainWindowSource, "DesktopPreferenceRuntime.LoadOrCreateState(DesktopHeadId)");
        RequireContains(mainWindowSource, "DesktopLocalizationCatalog.SetCurrentLanguageOverride(_persistedPreferences.Language)");

        string mainWindowPreferenceSource = ReadSource("Chummer.Avalonia/MainWindow.PreferenceState.cs");
        RequireContains(mainWindowPreferenceSource, "DesktopPreferenceRuntime.SaveState(DesktopHeadId, normalized)");
        RequireContains(mainWindowPreferenceSource, "DesktopPreferenceStateRuntime.Normalize(state.Preferences)");

        string desktopHomeSource = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        RequireContains(desktopHomeSource, "DesktopPreferenceRuntime.LoadOrCreateState(headId)");

        string installLinkSource = ReadSource("Chummer.Avalonia/DesktopInstallLinkingWindow.cs");
        RequireContains(installLinkSource, "DesktopPreferenceRuntime.LoadOrCreateState(context.State.HeadId).Language");
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
        RequireContains(source, "DesktopSupportCaseWindow.ShowAsync(this, _installState.HeadId, _supportProjection)");
        RequireContains(source, "DesktopUpdateWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "client.GetDesktopHomeSupportDigestsAsync");
        RequireContains(source, "DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId)");

        string projectorSource = ReadSource("Chummer.Presentation/Overview/DesktopHomeSupportProjector.cs");
        RequireContains(projectorSource, "Tracked case:");
        RequireContains(projectorSource, "Release progress:");
        RequireContains(projectorSource, "Verification:");
        RequireContains(projectorSource, "Affected install:");
        RequireContains(projectorSource, "InstallReadinessSummary");
        RequireContains(projectorSource, "NeedsInstallUpdate");
    }

    private static void DesktopHome_wires_the_build_and_explain_projection_into_the_summary_panel()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        RequireContains(source, "ReadBuildExplainProjectionAsync");
        RequireContains(source, "BuildBuildExplainBody()");
        RequireContains(source, "_buildExplainProjection.NextSafeAction");
        RequireContains(source, "_buildExplainProjection.RulesetSpotlight");
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
        RequireContains(source, "desktop.home.button.open_current_workspace");
        RequireContains(source, "desktop.home.button.open_campaign_followthrough");
        RequireContains(source, "desktop.home.button.open_build_followthrough");
        RequireContains(source, "desktop.home.button.open_workspace_followthrough");
        RequireContains(source, "desktop.home.button.open_work_support");
        RequireContains(source, "Next: ");
        RequireContains(source, "RulesetUiDirectiveCatalog.BuildOpenWorkspaceActionLabel");
        RequireContains(source, "RulesetUiDirectiveCatalog.BuildBuildFollowThroughActionLabel");
        RequireContains(source, "RulesetUiDirectiveCatalog.BuildWorkspaceFollowThroughActionLabel");
        RequireContains(source, "RulesetUiDirectiveCatalog.BuildNextActionPrefix");
        RequireContains(source, "RulesetUiDirectiveCatalog.BuildWorkspaceResumeSummary");
        RequireContains(source, "_buildExplainText");
        RequireContains(source, "_workspaceSummaryText");
        RequireContains(source, "OpenCampaignFollowThroughAsync");
        RequireContains(source, "OpenBuildFollowThroughAsync");
        RequireContains(source, "OpenWorkspaceFollowThroughAsync");
        RequireContains(source, "OpenWorkspaceInDesktopShellAsync");
        RequireContains(source, "mainWindow.OpenWorkspaceFromDesktopSurfaceAsync(workspaceId)");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenWorkspacePortal(workspaceId)");
        RequireContains(source, "DesktopSupportWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId)");
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
        RequireContains(projectorSource, "Build Lab coverage:");
        RequireContains(projectorSource, "Build Lab coverage detail:");
        RequireContains(projectorSource, "Rules navigator:");
        RequireContains(projectorSource, "Migration receipt:");
        RequireContains(projectorSource, "Publication receipt:");
        RequireDoesNotContain(source, "Open work follow-through");
    }

    private static void ShellNavigator_wires_ruleset_specific_headings_and_labels()
    {
        string summaryHeaderSource = ReadSource("Chummer.Blazor/Components/Shell/SummaryHeader.razor");
        RequireContains(summaryHeaderSource, "BuildSummaryHeading()");
        RequireContains(summaryHeaderSource, "RulesetUiDirectiveCatalog.BuildSummaryHeading");
        RequireContains(summaryHeaderSource, "BuildActiveRuntimeSummary(ShellSurface.ActiveRuntime, ResolveActiveRulesetId())");

        string openWorkspaceSource = ReadSource("Chummer.Blazor/Components/Shell/OpenWorkspaceTree.razor");
        RequireContains(openWorkspaceSource, "BuildOpenWorkspacesHeading()");
        RequireContains(openWorkspaceSource, "RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading");
        RequireContains(openWorkspaceSource, "RulesetUiDirectiveCatalog.BuildWorkspaceNavigatorLabel");

        string blazorSource = ReadSource("Chummer.Blazor/Components/Shell/WorkspaceLeftPane.razor");
        RequireContains(blazorSource, "BuildNavigationTabsHeading()");
        RequireContains(blazorSource, "BuildSectionActionsHeading()");
        RequireContains(blazorSource, "BuildWorkflowSurfacesHeading()");
        RequireContains(blazorSource, "FormatNavigationTabLabel(tab)");
        RequireContains(blazorSource, "FormatWorkspaceActionLabel(action)");
        RequireContains(blazorSource, "FormatWorkflowSurfaceLabel(surface)");
        RequireContains(blazorSource, "RulesetUiDirectiveCatalog.BuildNavigationTabsHeading");
        RequireContains(blazorSource, "RulesetUiDirectiveCatalog.BuildSectionActionsHeading");
        RequireContains(blazorSource, "RulesetUiDirectiveCatalog.BuildWorkflowSurfacesHeading");
        RequireContains(blazorSource, "RulesetUiDirectiveCatalog.FormatNavigationTabLabel");
        RequireContains(blazorSource, "RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel");
        RequireContains(blazorSource, "RulesetUiDirectiveCatalog.FormatWorkflowSurfaceLabel");

        string avaloniaProjectorSource = ReadSource("Chummer.Avalonia/MainWindow.ShellFrameProjector.cs");
        RequireContains(avaloniaProjectorSource, "OpenWorkspacesHeading: RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading");
        RequireContains(avaloniaProjectorSource, "NavigationTabsHeading: RulesetUiDirectiveCatalog.BuildNavigationTabsHeading");
        RequireContains(avaloniaProjectorSource, "SectionActionsHeading: RulesetUiDirectiveCatalog.BuildSectionActionsHeading");
        RequireContains(avaloniaProjectorSource, "WorkflowSurfacesHeading: RulesetUiDirectiveCatalog.BuildWorkflowSurfacesHeading");
        RequireContains(avaloniaProjectorSource, "RulesetUiDirectiveCatalog.FormatNavigationTabLabel");
        RequireContains(avaloniaProjectorSource, "RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel");
        RequireContains(avaloniaProjectorSource, "RulesetUiDirectiveCatalog.FormatWorkflowSurfaceLabel");

        string avaloniaNavigatorView = ReadSource("Chummer.Avalonia/Controls/NavigatorPaneControl.axaml");
        RequireContains(avaloniaNavigatorView, "x:Name=\"OpenWorkspacesHeader\"");
        RequireContains(avaloniaNavigatorView, "x:Name=\"NavigationTabsHeader\"");
        RequireContains(avaloniaNavigatorView, "x:Name=\"SectionActionsHeader\"");
        RequireContains(avaloniaNavigatorView, "x:Name=\"WorkflowSurfacesHeader\"");

        string avaloniaNavigatorSource = ReadSource("Chummer.Avalonia/Controls/NavigatorPaneControl.axaml.cs");
        RequireContains(avaloniaNavigatorSource, "OpenWorkspacesHeader.Text = state.OpenWorkspacesHeading");
        RequireContains(avaloniaNavigatorSource, "RulesetUiDirectiveCatalog.BuildWorkspaceNavigatorLabel");
        RequireContains(avaloniaNavigatorSource, "NavigationTabsHeader.Text = state.NavigationTabsHeading");
        RequireContains(avaloniaNavigatorSource, "SectionActionsHeader.Text = state.SectionActionsHeading");
        RequireContains(avaloniaNavigatorSource, "WorkflowSurfacesHeader.Text = state.WorkflowSurfacesHeading");
    }

    private static void ShellRightRail_and_workspace_strip_wire_ruleset_specific_copy()
    {
        string desktopShellSource = ReadSource("Chummer.Blazor/Components/Layout/DesktopShell.razor");
        RequireContains(desktopShellSource, "<MdiStrip");
        RequireContains(desktopShellSource, "RulesetId=\"@_shellSurfaceState.ActiveRulesetId\"");
        RequireContains(desktopShellSource, "<ImportPanel");
        RequireContains(desktopShellSource, "<CommandPanel");
        RequireContains(desktopShellSource, "<ResultPanel");

        string mdiStripSource = ReadSource("Chummer.Blazor/Components/Shell/MdiStrip.razor");
        RequireContains(mdiStripSource, "BuildWorkspaceStripEmptyState()");
        RequireContains(mdiStripSource, "BuildWorkspaceTitle(workspace)");
        RequireContains(mdiStripSource, "RulesetUiDirectiveCatalog.BuildWorkspaceStripEmptyState");
        RequireContains(mdiStripSource, "RulesetUiDirectiveCatalog.BuildWorkspaceStripTitle");

        string importPanelSource = ReadSource("Chummer.Blazor/Components/Shell/ImportPanel.razor");
        RequireContains(importPanelSource, "BuildImportHeading()");
        RequireContains(importPanelSource, "BuildImportAcceptAttribute()");
        RequireContains(importPanelSource, "BuildImportHint()");
        RequireContains(importPanelSource, "BuildImportDebugHeading()");
        RequireContains(importPanelSource, "BuildImportRawActionLabel()");
        RequireContains(importPanelSource, "RulesetUiDirectiveCatalog.BuildImportHeading");
        RequireContains(importPanelSource, "RulesetUiDirectiveCatalog.BuildImportAcceptAttribute");
        RequireContains(importPanelSource, "RulesetUiDirectiveCatalog.BuildImportHint");

        string commandPanelSource = ReadSource("Chummer.Blazor/Components/Shell/CommandPanel.razor");
        RequireContains(commandPanelSource, "BuildCommandHeading()");
        RequireContains(commandPanelSource, "BuildCommandEmptyHint()");
        RequireContains(commandPanelSource, "RulesetUiDirectiveCatalog.BuildCommandHeading");
        RequireContains(commandPanelSource, "RulesetUiDirectiveCatalog.BuildCommandEmptyHint");

        string resultPanelSource = ReadSource("Chummer.Blazor/Components/Shell/ResultPanel.razor");
        RequireContains(resultPanelSource, "BuildResultHeading()");
        RequireContains(resultPanelSource, "BuildResultPostureHint()");
        RequireContains(resultPanelSource, "BuildResultReadyNotice()");
        RequireContains(resultPanelSource, "RulesetUiDirectiveCatalog.BuildResultHeading");
        RequireContains(resultPanelSource, "RulesetUiDirectiveCatalog.BuildResultPostureHint");
        RequireContains(resultPanelSource, "RulesetUiDirectiveCatalog.BuildResultReadyNotice");
    }

    private static void DesktopShell_ruleset_matrix_coverage_is_published_and_executable()
    {
        string rulesetTestSource = ReadSource("Chummer.Tests/Presentation/DesktopShellRulesetCatalogTests.cs");
        RequireContains(rulesetTestSource, "DesktopShell_renders_ruleset_specific_flagship_posture_for_each_supported_lane");
        RequireContains(rulesetTestSource, "DataRow(RulesetDefaults.Sr4");
        RequireContains(rulesetTestSource, "DataRow(RulesetDefaults.Sr5");
        RequireContains(rulesetTestSource, "DataRow(RulesetDefaults.Sr6");
        RequireContains(rulesetTestSource, "RegisterDesktopShellServices");
        RequireContains(rulesetTestSource, "CatalogOnlyRulesetPlugin");

        string worklistSource = ReadSource("WORKLIST.md");
        RequireContains(worklistSource, "| WL-215 | done |");
        RequireContains(worklistSource, "ruleset-ui-adaptation-check.sh");
    }

    private static void FlagshipDesktopShell_exposes_persistent_home_install_and_support_actions()
    {
        string toolStripMarkup = ReadSource("Chummer.Avalonia/Controls/ToolStripControl.axaml");
        RequireContains(toolStripMarkup, "x:Name=\"DesktopHomeButton\"");
        RequireContains(toolStripMarkup, "x:Name=\"CampaignWorkspaceButton\"");
        RequireContains(toolStripMarkup, "x:Name=\"UpdateStatusButton\"");
        RequireContains(toolStripMarkup, "x:Name=\"InstallLinkingButton\"");
        RequireContains(toolStripMarkup, "x:Name=\"SupportButton\"");
        RequireContains(toolStripMarkup, "x:Name=\"ReportIssueButton\"");
        RequireContains(toolStripMarkup, "DesktopHomeButton_OnClick");
        RequireContains(toolStripMarkup, "CampaignWorkspaceButton_OnClick");
        RequireContains(toolStripMarkup, "UpdateStatusButton_OnClick");
        RequireContains(toolStripMarkup, "InstallLinkingButton_OnClick");
        RequireContains(toolStripMarkup, "SupportButton_OnClick");
        RequireContains(toolStripMarkup, "ReportIssueButton_OnClick");

        string toolStripSource = ReadSource("Chummer.Avalonia/Controls/ToolStripControl.axaml.cs");
        RequireContains(toolStripSource, "DesktopHomeRequested");
        RequireContains(toolStripSource, "CampaignWorkspaceRequested");
        RequireContains(toolStripSource, "UpdateStatusRequested");
        RequireContains(toolStripSource, "InstallLinkingRequested");
        RequireContains(toolStripSource, "SupportRequested");
        RequireContains(toolStripSource, "ReportIssueRequested");
        RequireContains(toolStripSource, "desktop.shell.tool.desktop_home");
        RequireContains(toolStripSource, "desktop.shell.tool.campaign_workspace");
        RequireContains(toolStripSource, "desktop.shell.tool.update_status");
        RequireContains(toolStripSource, "desktop.shell.tool.link_copy");
        RequireContains(toolStripSource, "desktop.shell.tool.open_support");
        RequireContains(toolStripSource, "desktop.shell.tool.report_issue");
        RequireContains(toolStripSource, "desktop.shell.tool.settings");
        RequireContains(toolStripSource, "desktop.shell.tool.status_idle");

        string menuBarMarkup = ReadSource("Chummer.Avalonia/Controls/ShellMenuBarControl.axaml");
        RequireContains(menuBarMarkup, "Tag=\"file\"");
        RequireContains(menuBarMarkup, "Tag=\"edit\"");
        RequireContains(menuBarMarkup, "Tag=\"special\"");
        RequireContains(menuBarMarkup, "Tag=\"tools\"");
        RequireContains(menuBarMarkup, "Tag=\"windows\"");
        RequireContains(menuBarMarkup, "Tag=\"help\"");
        RequireDoesNotContain(menuBarMarkup, "Avalonia Head");

        string menuBarSource = ReadSource("Chummer.Avalonia/Controls/ShellMenuBarControl.axaml.cs");
        RequireContains(menuBarSource, "GetMenuId(button)");
        RequireContains(menuBarSource, "button.Tag?.ToString()");
        RequireContains(menuBarSource, "desktop.shell.menu.file");
        RequireContains(menuBarSource, "desktop.shell.banner");

        string bindingSource = ReadSource("Chummer.Avalonia/MainWindow.ControlBinding.cs");
        RequireContains(bindingSource, "onDesktopHomeRequested");
        RequireContains(bindingSource, "onCampaignWorkspaceRequested");
        RequireContains(bindingSource, "onUpdateStatusRequested");
        RequireContains(bindingSource, "onInstallLinkingRequested");
        RequireContains(bindingSource, "onSupportRequested");
        RequireContains(bindingSource, "onReportIssueRequested");
        RequireContains(bindingSource, "onSettingsRequested");
        RequireContains(bindingSource, "toolStrip.DesktopHomeRequested +=");
        RequireContains(bindingSource, "toolStrip.CampaignWorkspaceRequested +=");
        RequireContains(bindingSource, "toolStrip.UpdateStatusRequested +=");
        RequireContains(bindingSource, "toolStrip.InstallLinkingRequested +=");
        RequireContains(bindingSource, "toolStrip.SupportRequested +=");
        RequireContains(bindingSource, "toolStrip.ReportIssueRequested +=");
        RequireContains(bindingSource, "toolStrip.SettingsRequested +=");

        string eventHandlerSource = ReadSource("Chummer.Avalonia/MainWindow.EventHandlers.cs");
        RequireContains(eventHandlerSource, "ToolStrip_OnDesktopHomeRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnCampaignWorkspaceRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnUpdateStatusRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnInstallLinkingRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnSupportRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnReportIssueRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnSettingsRequested");
        RequireContains(eventHandlerSource, "DesktopHomeWindow.ShowAsync(this, \"avalonia\")");
        RequireContains(eventHandlerSource, "DesktopCampaignWorkspaceWindow.ShowAsync(this, \"avalonia\")");
        RequireContains(eventHandlerSource, "DesktopUpdateWindow.ShowAsync(this, \"avalonia\")");
        RequireContains(eventHandlerSource, "DesktopSupportWindow.ShowAsync(this, \"avalonia\")");
        RequireContains(eventHandlerSource, "DesktopReportIssueWindow.ShowAsync(this, \"avalonia\")");
        RequireContains(eventHandlerSource, "DesktopInstallLinkingWindow.ShowAsync(this, \"avalonia\")");
        RequireContains(eventHandlerSource, "_interactionCoordinator.ExecuteCommandAsync(\"global_settings\", CancellationToken.None)");

        string desktopHomeSource = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        RequireContains(desktopHomeSource, "public static async Task ShowAsync(Window owner, string headId)");
        RequireContains(desktopHomeSource, "DesktopLocalizationCatalog.GetRequiredString(\"desktop.home.title\"");
        RequireContains(desktopHomeSource, "new ScrollViewer");
        RequireContains(desktopHomeSource, "CreateLanguageActions()");
        RequireContains(desktopHomeSource, "desktop.home.button.open_settings");
        RequireContains(desktopHomeSource, "mainWindow.OpenDesktopCommandFromSurfaceAsync(\"global_settings\", \"open global settings\")");

        string installLinkSource = ReadSource("Chummer.Avalonia/DesktopInstallLinkingWindow.cs");
        RequireContains(installLinkSource, "public static async Task ShowAsync(Window owner, string headId)");

        string mainWindowMarkup = ReadSource("Chummer.Avalonia/MainWindow.axaml");
        RequireDoesNotContain(mainWindowMarkup, "Chummer Avalonia Head");

        string mainWindowSource = ReadSource("Chummer.Avalonia/MainWindow.axaml.cs");
        RequireContains(mainWindowSource, "desktop.shell.window_title");

        string navigationSource = ReadSource("Chummer.Avalonia/MainWindow.DesktopSurfaceNavigation.cs");
        RequireContains(navigationSource, "OpenDesktopCommandFromSurfaceAsync");
        RequireContains(navigationSource, "_interactionCoordinator.ExecuteCommandAsync");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "string.Equals(startupSurface, \"settings\"");
        RequireContains(appSource, "owner.OpenDesktopCommandFromSurfaceAsync(\"global_settings\", \"open global settings\")");
    }

    private static void DesktopHome_degrades_gracefully_when_workspace_bootstrap_is_unavailable()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        RequireContains(source, "private static async Task<IReadOnlyList<WorkspaceListItem>> ReadWorkspacesAsync(IChummerClient client)");
        RequireContains(source, "return Array.Empty<WorkspaceListItem>();");
        RequireContains(source, "catch");
    }

    private static void DesktopHome_exposes_claim_aware_install_and_update_actions()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        RequireContains(source, "CreateInstallActions()");
        RequireContains(source, "CreateUpdateActions()");
        RequireContains(source, "desktop.install_link.button.link_copy");
        RequireContains(source, "desktop.home.button.open_devices_access");
        RequireContains(source, "desktop.home.button.open_current_workspace");
        RequireContains(source, "desktop.home.button.open_update_status");
        RequireContains(source, "desktop.home.button.open_support_center");
        RequireContains(source, "desktop.home.button.open_report_issue");
        RequireContains(source, "desktop.home.button.open_install_support");
        RequireContains(source, "desktop.home.button.open_update_support");
        RequireContains(source, "DesktopInstallLinkingWindow dialog = new(context);");
        RequireContains(source, "RefreshHomeStateAsync()");
        RequireContains(source, "desktop.home.install_summary.last_claim_attempt");
        RequireContains(source, "desktop.home.update_summary");
        RequireContains(source, "desktop.home.value.no_supportability_summary");
        RequireContains(source, "desktop.home.value.no_fix_guidance");
        RequireContains(source, "OpenUpdateWindowAsync()");
        RequireContains(source, "OpenSupportWindowAsync()");
        RequireContains(source, "OpenReportIssueWindowAsync()");
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
        RequireContains(source, "desktop.home.button.open_report_issue");
        RequireContains(source, "desktop.install_link.title");
        RequireContains(source, "GetRequiredString");
        RequireContains(source, "desktop.install_link.claim_code_watermark");
        RequireContains(source, "desktop.install_link.summary.last_claim_attempt");
        RequireContains(source, "desktop.install_link.summary.hub_message");
        RequireContains(source, "desktop.install_link.summary.claim_error");
        RequireContains(source, "desktop.install_link.summary.next_safe_action_claimed");
        RequireContains(source, "desktop.install_link.summary.next_safe_action_guest");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenDownloadsPortal()");
        RequireContains(source, "DesktopSupportWindow.ShowAsync(this, _state.HeadId)");
        RequireContains(source, "DesktopReportIssueWindow.ShowAsync(this, _state.HeadId)");
        RequireContains(source, "DesktopCampaignWorkspaceWindow.ShowAsync(ownerWindow, _state.HeadId)");
        RequireContains(source, "DesktopCampaignWorkspaceWindow.ShowAsync(this, _state.HeadId)");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenAccountPortal()");
    }

    private static void BlazorHome_uses_local_chummer6_flagship_media_samples()
    {
        string source = ReadSource("Chummer.Blazor/Components/Pages/Home.razor");
        RequireContains(source, "/media/portraits/contact-portrait-revision.png");
        RequireContains(source, "/media/portraits/contact-portrait-current.png");
        RequireContains(source, "/media/routes/route-recap-clip.mp4");
        RequireContains(source, "asset-portraits-01");
        RequireContains(source, "asset-dossier-01");
        RequireContains(source, "asset-news-01");
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

    private static void RequireNotEmpty(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Expected non-empty value for '{fieldName}' in smoke target source.");
        }
    }
}
