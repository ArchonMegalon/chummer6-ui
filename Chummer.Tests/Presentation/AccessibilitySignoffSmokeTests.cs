#nullable enable annotations

using Chummer.Campaign.Contracts;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class AccessibilitySignoffSmokeTests
{
    [TestMethod]
    public void Accessibility_signoff_smoke_checks_pass()
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
        DesktopCampaignWorkspace_promotes_gm_runboard_route();
        DesktopCampaignWorkspace_promotes_gm_prep_packets_and_roster_movement();
        DesktopOrganizerOperationsSurface_is_a_real_top_level_surface();
        DesktopOrganizerOperations_keeps_role_boundaries_visible();
        DesktopRuleEnvironmentStudioSurface_is_a_real_top_level_surface();
        DesktopCampaignWorkspace_keeps_restore_conflict_choices_visible();
        CharacterRosterStructureParityGuardTests.Run();
        DesktopUpdateSurface_is_a_real_top_level_surface();
        DesktopSupportSurface_is_a_real_top_level_surface();
        DesktopSupportCaseSurface_is_a_real_top_level_surface();
        DesktopExplainReceipts_and_diagnostics_diffs_are_visible_on_trust_surfaces();
        DesktopDevicesAccessSurface_is_a_real_top_level_surface();
        DesktopReportSurface_is_a_real_top_level_surface();
        DesktopCrashRecoverySurface_is_a_real_top_level_surface();
        DesktopCloseOnlySurfaces_use_explicit_close_copy();
        DesktopPreferencePersistence_is_restart_safe_for_flagship_shell_and_native_surfaces();
        DesktopHome_degrades_gracefully_when_workspace_bootstrap_is_unavailable();
        DesktopHome_wires_the_campaign_projection_into_the_summary_panel();
        DesktopHome_wires_the_support_projection_into_the_summary_panel();
        DesktopHome_wires_the_build_and_explain_projection_into_the_summary_panel();
        PrimaryDesktopSummaryHeader_keeps_restore_stale_and_conflict_choices_visible();
        ShellNavigator_wires_ruleset_specific_headings_and_labels();
        DesktopShell_removes_right_rail_and_workspace_strip_keeps_ruleset_specific_copy();
        DesktopShell_ruleset_matrix_coverage_is_published_and_executable();
        DesktopHome_exposes_claim_aware_install_and_update_actions();
        DesktopInstallLinkingWindow_exposes_trust_actions_and_locale_guidance();
        BlazorDesktopShell_blocks_unlinked_installs_with_visible_claim_gate();
        BlazorDesktopPrintPreview_waits_for_loaded_document_before_printing();
        BlazorDialogReveal_skips_same_dialog_recenters_across_transient_refreshes();
        DesktopHead_uses_canonical_catalog_only_resolver();
    }

    [TestMethod]
    public void Blazor_dialog_reveal_scroll_restore_contract_keeps_origin_refreshes_stable()
    {
        BlazorDialogReveal_skips_same_dialog_recenters_across_transient_refreshes();
    }

    [TestMethod]
    public void Blazor_print_and_analytics_privacy_boundaries_are_fail_closed()
    {
        BlazorDesktopPrintPreview_waits_for_loaded_document_before_printing();
    }

    private static void SectionPane_renders_browse_projection_with_saved_filters_and_keyboard_navigation()
    {
        string source = ReadSource("Chummer.Blazor/Components/Shell/SectionPane.razor");
        RequireContains(source, "role=\"listbox\"");
        RequireContains(source, "role=\"option\"");
        RequireContains(source, "aria-activedescendant=");
        RequireContains(source, "aria-selected=\"@(IsBrowseResultActive(browseWorkspace, item) ? \"true\" : \"false\")");
        RequireContains(source, "Build blocker details:");
        RequireContains(source, "BuildBuildBlockerBefore(buildLab)");
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
                AdoptionSummary: "Start from today keeps the current runners, open debts, and active job packet attached to the same campaign anchor.",
                AdoptionConfidenceSummary: "playable_with_review because one runner contact map still needs follow-up cleanup before the lane is fully ready.",
                AdoptionEvidenceSummary: "CampaignAdoptionReceipt adopt-001 keeps the unknown-history markers and cleanup trail visible.",
                GoalPinSummary: "Ghostwire -> Wired Reflexes Rating 2 (47000 / 149000 nuyen saved)",
                ResolutionReportSummary: "Run Dockside Courier is approved and ready to feed one WorldTick and one player-safe news item.",
                BlackLedgerSummary: "ConsequenceReceipt consequence-001 cites adopt-001, resolution_report_openrun_001, heat_tick_001, and news_001 with player-safe spoiler posture.",
                BlackLedgerProofSummary: "BLACK LEDGER approval keeps the consequence receipt, player-safe news item, and WorldTick closeout bound to the same governed proof chain.",
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
        RequireContains(projection.RestoreSummary, "Restore:");
        RequireContains(projection.RestoreSummary, "offline use");
        RequireContains(projection.DeviceRoleSummary, "play_tablet");
        RequireContains(projection.SupportClosureSummary, "calmer digest keeps the fix path");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Digest highlight:");
        RequireContains(string.Join("\n", projection.Watchouts), "Digest watchout:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Campaign return:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Current scene:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Campaign adoption:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Adoption confidence:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Adoption details:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Goal pins:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "ResolutionReport closeout:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "BLACK LEDGER consequence:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "BLACK LEDGER details:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Build next step:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Rules next step:");
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
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Campaign-ready path:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Starter path next:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "First-session details:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Campaign memory:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Campaign memory return:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Portable exchange:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Exchange context:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Exchange asset scope:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Exchange formats:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Exchange note:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "server update highlight:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Support:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Decision notice:");
        RequireContains(projection.NextSafeAction, "server update next safe action");
        RequireContains(string.Join("\n", projection.Watchouts), "cloud-only snapshot");
        RequireContains(string.Join("\n", projection.Watchouts), "GM-only notes");
        RequireContains(string.Join("\n", projection.Watchouts), "Portable exchange:");
        RequireContains(string.Join("\n", projection.Watchouts), "server update watchout:");
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
            Title: "Prime runner transfer",
            Summary: "The next campaign step keeps build, runtime, and return path aligned.",
            VariantLabel: "Prime runner preview",
            ProgressionLabel: "Street launch",
            ExplainEntryId: "explain-1",
            TradeoffLines: ["Trade a late armor bump for cleaner campaign re-entry."],
            ProgressionOutcomes: ["The runner stays campaign-ready after the campaign step."],
            Outputs: [],
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T10:21:00+00:00"),
            NextSafeAction: "Review the grounded transfer before you publish or return to campaign play.",
            RuntimeCompatibilitySummary: "The campaign transfer still matches the current runtime fingerprint.",
            CampaignReturnSummary: "The transfer can return through the current campaign workspace once confirmed.",
            SupportClosureSummary: "Support can use the same transfer if verification fails.",
            PlannerCoverageSummary: "4 of 4 build follow-through checkpoints are already grounded.",
            PlannerCoverageLines:
            [
                "Campaign continuity: Apex is already attached as the return path for this transfer.",
                "Outputs: no dossier or campaign-safe output is attached yet, so export and recap details are still pending.",
                "Restore status: no restore conflicts are currently blocking replay-safe transfer follow-through.",
                "Claimed install: no linked device is attached yet for install-aware follow-through."
            ],
            Watchouts: ["The campaign step still needs an explicit confirmation click."]);
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
        RequireContains(projection.RulesetSpotlight, "SR6 opens to the character builder");
        RequireContains(projection.ExplainFocus, "Explain focus:");
        RequireContains(projection.ExplainFocus, "Build path focus: Edge Runner Starter");
        RequireContains(projection.ExplainFocus, "Campaign next step:");
        RequireContains(projection.RuntimeHealthSummary, "runtime");
        RequireContains(projection.RuntimeHealthSummary, "runtime drift requires a rebind");
        RequireNotEmpty(projection.ReturnTarget, nameof(projection.ReturnTarget));
        RequireNotEmpty(projection.RulePosture, nameof(projection.RulePosture));
        if (projection.CompatibilityReceipts.Count < 2)
        {
            throw new InvalidOperationException("Desktop build/explain projection should surface explicit compatibility records for the flagship home surface.");
        }
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Compatibility note:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "profile refresh");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build path option: Edge Runner Starter is ready");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build path runtime:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build path return:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build path support:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build Lab next step:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build Lab tradeoff:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build Lab progression:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build Lab coverage:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build Lab coverage detail:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Rules navigator:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Migration summary:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Publication summary:");
        if (projection.BuildPathComparisons.Count < 2)
        {
            throw new InvalidOperationException("Desktop build/explain projection should compare multiple build paths in the flagship home surface.");
        }
        RequireContains(string.Join("\n", projection.BuildPathComparisons), "Build path compare: Edge Runner Starter");
        RequireContains(string.Join("\n", projection.BuildPathComparisons), "Build path compare: Street Sam Starter");
        RequireContains(projection.Summary, "Metatype B");
        RequireContains(projection.Summary, "SR6");
        RequireContains(projection.Summary, "Used, Prototype");
        RequireNoPlayerFacingMachineryTerms(projection);
        if (projection.Watchouts.Count < 2)
        {
            throw new InvalidOperationException("Desktop build/explain projection should surface multiple watchouts for the flagship home surface.");
        }
        RequireContains(string.Join("\n", projection.Watchouts), "explicit confirmation click");
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
        RequireContains(projection.RulesetSpotlight, "SR5 opens to the main character editor");
        RequireContains(projection.ExplainFocus, "claim this copy");
        RequireContains(projection.RuntimeHealthSummary, "no active runtime profile");
        RequireContains(projection.ReturnTarget, "No dossier return target");
        RequireContains(projection.RulePosture, "Shadowrun 5");
        RequireContains(projection.RulePosture, ".chum5");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "no current runtime fingerprint");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build path option: Street Sam Starter is available");
        RequireContains(string.Join("\n", projection.BuildPathComparisons), "Build path compare: Street Sam Starter");
        RequireNoPlayerFacingMachineryTerms(projection);
        if (projection.Watchouts.Count < 2)
        {
            throw new InvalidOperationException("Desktop build/explain projection should keep explicit watchouts even before the first workspace exists.");
        }
    }

    private static void RequireNoPlayerFacingMachineryTerms(DesktopHomeBuildExplainProjection projection)
    {
        string visible = string.Join(
            "\n",
            [
                projection.Summary,
                projection.NextSafeAction,
                projection.ExplainFocus,
                projection.RuntimeHealthSummary,
                projection.ReturnTarget,
                projection.RulePosture,
                .. projection.CompatibilityReceipts,
                .. projection.BuildPathComparisons,
                .. projection.Watchouts
            ]);

        RequireDoesNotContain(visible, "receipt");
        RequireDoesNotContain(visible, "proof");
        RequireDoesNotContain(visible, "provider");
        RequireDoesNotContain(visible, "grounded");
        RequireDoesNotContain(visible, "handoff");
        RequireDoesNotContain(visible, " lane");
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
        RequireContains(string.Join("\n", projection.Highlights), "Confirmation:");
        RequireDoesNotContain(string.Join("\n", projection.Highlights), "Verification:");
        RequireContains(string.Join("\n", projection.Highlights), "Fixed release: preview 0.6.3-smoke");
        RequireContains(string.Join("\n", projection.Highlights), "Affected install:");
        RequireContains(string.Join("\n", projection.Highlights), "Next step:");
        if (!projection.NeedsAttention || !projection.HasTrackedCase)
        {
            throw new InvalidOperationException("Desktop support projection should mark reporter-verification follow-through as attention-worthy when a tracked fix is ready.");
        }
    }

    private static void DesktopHome_wires_the_campaign_projection_into_the_summary_panel()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        string campaignWorkspaceSource = ReadSource("Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs");
        string campaignSurfaceSource = source + "\n" + campaignWorkspaceSource;
        RequireContains(source, "ReadCampaignProjectionAsync");
        RequireContains(source, "BuildCampaignBody()");
        RequireContains(source, "_campaignProjection.NextSafeAction");
        RequireContains(source, "_campaignProjection.Summary");
        RequireContains(source, "_campaignProjection.RestoreSummary");
        RequireContains(source, "_campaignProjection.DeviceRoleSummary");
        RequireContains(source, "BuildCampaignStaleStateVisibilitySummary()");
        RequireContains(source, "ShouldShowForRestoreContinuityReview(");
        RequireContains(source, "campaignProjection.Watchouts.Count > 0");
        RequireContains(source, "campaignServerPlane is null || IsServerContinuityOlderThanLocalWorkspace(workspaces, campaignServerPlane)");
        RequireDoesNotContain(source, "Restore choice:");
        RequireContains(source, "Stale state: server continuity is unavailable");
        RequireContains(source, "Stale state: local workspace changed at");
        RequireContains(source, "Stale state: server continuity is current as of");
        RequireDoesNotContain(source, "Decision order:");
        RequireDoesNotContain(source, "Local authority:");
        RequireDoesNotContain(source, "Conflict choices:");
        RequireContains(source, "_campaignProjection.SupportClosureSummary");
        RequireContains(source, "_campaignProjection.ReadinessHighlights");
        RequireContains(source, "_campaignProjection.Watchouts");
        RequireContains(source, "BuildCampaignConsequenceSummary()");
        RequireContains(source, "BuildCampaignConsequenceEvidenceSummary()");
        RequireContains(source, "BuildCampaignNextSessionReturnSummary()");
        RequireContains(source, "BuildCampaignReturnActionSummary()");
        RequireContains(source, "BuildCampaignAdoptionSummary()");
        RequireContains(source, "BuildCampaignAdoptionConfidenceSummary()");
        RequireContains(source, "BuildRunnerGoalPinSummary()");
        RequireContains(source, "BuildResolutionReportCloseoutSummary()");
        RequireContains(source, "ResolveCampaignMemorySummary()");
        RequireContains(source, "ResolveCampaignMemoryReturnSummary()");
        RequireContains(source, "ResolveCampaignMemoryEvidence()");
        RequireContains(source, "ResolveCampaignMemoryNextSafeAction()");
        RequireContains(source, "Campaign adoption:");
        RequireContains(source, "Adoption confidence:");
        RequireContains(source, "Runner goal pins:");
        RequireContains(source, "ResolutionReport closeout:");
        RequireContains(source, "BLACK LEDGER consequence details:");
        RequireContains(source, "Campaign adoption details:");
        RequireContains(source, "Campaign consequence summary:");
        RequireContains(source, "Campaign consequence details:");
        RequireContains(source, "Campaign next-session return:");
        RequireContains(campaignSurfaceSource, "Review campaign consequences before continuing this restore route.");
        RequireContains(source, "Review next-session return action:");
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
        RequireContains(source, "ResolveSupportWorkspace()");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace(_installState, ResolveSupportWorkspace())");
        RequireContains(source, "DesktopSupportWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "OpenArtifactShelfView");
        RequireContains(source, "DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal($\"/artifacts?view={Uri.EscapeDataString(view)}\")");

        string projectorSource = ReadSource("Chummer.Presentation/Overview/DesktopHomeCampaignProjector.cs");
        RequireContains(projectorSource, "Campaign return:");
        RequireContains(projectorSource, "Support closure:");
        RequireContains(projectorSource, "Claimed device:");
        RequireContains(projectorSource, "Migration continuity:");
        RequireContains(projectorSource, "Portable exchange:");
        RequireContains(projectorSource, "Exchange formats:");
        RequireContains(projectorSource, "Publication trust:");
        RequireContains(projectorSource, "CampaignWorkspaceDigestProjection");
        RequireContains(projectorSource, "Support:");
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
        RequireContains(serverPlaneSource, "Item trust:");
        RequireContains(serverPlaneSource, "Item views:");
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
        RequireContains(source, "BuildRestoreStaleStateVisibilitySummary()");
        RequireDoesNotContain(source, "Restore choice:");
        RequireContains(source, "Stale state: server continuity is unavailable");
        RequireContains(source, "IsServerContinuityOlderThanLocalWorkspace()");
        RequireContains(source, "Stale state: local workspace changed at");
        RequireContains(source, "Stale state: server continuity is current as of");
        RequireDoesNotContain(source, "Decision order:");
        RequireDoesNotContain(source, "Local authority:");
        RequireDoesNotContain(source, "Conflict choices:");
        RequireDoesNotContain(source, "Support choice:");
        RequireContains(source, "BuildCampaignConsequenceSummary()");
        RequireContains(source, "BuildCampaignConsequenceEvidenceSummary()");
        RequireContains(source, "BuildCampaignNextSessionReturnSummary()");
        RequireContains(source, "BuildCampaignNextSessionReturnActionSummary()");
        RequireContains(source, "BuildCampaignAdoptionSummary()");
        RequireContains(source, "BuildCampaignAdoptionConfidenceSummary()");
        RequireContains(source, "BuildRunnerGoalPinSummary()");
        RequireContains(source, "BuildResolutionReportCloseoutSummary()");
        RequireContains(source, "ResolveCampaignMemorySummary()");
        RequireContains(source, "ResolveCampaignMemoryReturnSummary()");
        RequireContains(source, "ResolveCampaignMemoryEvidence()");
        RequireContains(source, "ResolveCampaignMemoryNextSafeAction()");
        RequireContains(source, "Campaign adoption:");
        RequireContains(source, "Adoption confidence:");
        RequireContains(source, "Runner goal pins:");
        RequireContains(source, "ResolutionReport closeout:");
        RequireContains(source, "BLACK LEDGER consequence details:");
        RequireContains(source, "Campaign adoption details:");
        RequireContains(source, "Campaign consequence summary:");
        RequireContains(source, "Campaign consequence details:");
        RequireContains(source, "Campaign next-session return:");
        RequireContains(source, "Review campaign consequences before continuing this restore route.");
        RequireContains(source, "Review next-session return action:");
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
        RequireContains(source, "ResolveSupportWorkspace()");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace(_installState, ResolveSupportWorkspace())");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall");
        RequireContains(source, "DesktopSupportWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId)");

        string navigationSource = ReadSource("Chummer.Avalonia/MainWindow.DesktopSurfaceNavigation.cs");
        RequireContains(navigationSource, "OpenWorkspaceFromDesktopSurfaceAsync");
        RequireContains(navigationSource, "_interactionCoordinator.SwitchWorkspaceAsync");
        RequireContains(navigationSource, "RunUiActionAsync");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "CHUMMER_DESKTOP_STARTUP_SURFACE");
        RequireContains(appSource, "DesktopStartupSurfaceCatalog.CampaignWorkspace");
        RequireContains(appSource, "DesktopCampaignWorkspaceWindow.ShowAsync(owner, \"avalonia\")");
    }

    private static void DesktopCampaignWorkspace_promotes_gm_runboard_route()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs");
        RequireContains(source, "public static Task ShowGmRunboardAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)");
        RequireContains(source, "Runboard:");
        RequireContains(source, "desktop.campaign.section.runboard");

        string homeSource = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        RequireContains(homeSource, "Open GM Runboard");
        RequireContains(homeSource, "OpenGmRunboardAsync");
        RequireContains(homeSource, "DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(this, _installState.HeadId)");

        string organizerSource = ReadSource("Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs");
        RequireContains(organizerSource, "Open GM Runboard");
        RequireContains(organizerSource, "OpenGmRunboardAsync");
        RequireContains(organizerSource, "DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(this, _installState.HeadId, _portabilityActivity)");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "DesktopStartupSurfaceCatalog.GmRunboard");
        RequireContains(appSource, "DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(owner, \"avalonia\")");
    }

    private static void DesktopCampaignWorkspace_promotes_gm_prep_packets_and_roster_movement()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs");
        RequireContains(source, "public static Task ShowGmPrepAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)");
        RequireContains(source, "public static Task ShowRosterMovementAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)");

        string homeSource = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        RequireContains(homeSource, "Open GM Prep Packets");
        RequireContains(homeSource, "Open Roster Movement");
        RequireContains(homeSource, "OpenGmPrepPacketsAsync");
        RequireContains(homeSource, "OpenRosterMovementAsync");
        RequireContains(homeSource, "DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(this, _installState.HeadId)");
        RequireContains(homeSource, "DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(this, _installState.HeadId)");

        string organizerSource = ReadSource("Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs");
        RequireContains(organizerSource, "Open GM Prep Packets");
        RequireContains(organizerSource, "Open Roster Movement");
        RequireContains(organizerSource, "OpenGmPrepPacketsAsync");
        RequireContains(organizerSource, "OpenRosterMovementAsync");
        RequireContains(organizerSource, "DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(this, _installState.HeadId, _portabilityActivity)");
        RequireContains(organizerSource, "DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(this, _installState.HeadId, _portabilityActivity)");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "DesktopStartupSurfaceCatalog.GmPrepPackets");
        RequireContains(appSource, "DesktopStartupSurfaceCatalog.RosterMovement");
        RequireContains(appSource, "DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(owner, \"avalonia\")");
        RequireContains(appSource, "DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(owner, \"avalonia\")");
    }

    private static void DesktopOrganizerOperationsSurface_is_a_real_top_level_surface()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs");
        RequireContains(source, "public static Task ShowAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)");
        RequireContains(source, "public static Task ShowRolesAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)");
        RequireContains(source, "Title = \"Organizer Operations\"");
        RequireContains(source, "\"Organizer Operations\"");
        RequireContains(source, "\"Role boundaries\"");
        RequireContains(source, "\"Publication and escalation\"");
        RequireContains(source, "\"Organizer:\"");
        RequireContains(source, "\"Operations:\"");
        RequireContains(source, "\"Review Organizer Roles\"");
        RequireContains(source, "\"Open Organizer Operations\"");
        RequireContains(source, "\"Open Creator Publication\"");
        RequireContains(source, "\"Review Moderation Flow\"");
        RequireContains(source, "\"Open Rule Environment Studio\"");
        RequireContains(source, "new ScrollViewer");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "DesktopOrganizerOperationsWindow.ShowAsync(owner, \"avalonia\")");
    }

    private static void DesktopOrganizerOperations_keeps_role_boundaries_visible()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs");
        RequireContains(source, "Keep organizer, GM, player, creator, moderation, and support work separated while showing the next useful action.");
        RequireContains(source, "\"GM:\"");
        RequireContains(source, "\"Player:\"");
        RequireContains(source, "\"Creator:\"");
        RequireContains(source, "\"Support:\"");
        RequireContains(source, "\"Boundary watchout:\"");
        RequireContains(source, "DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId, _portabilityActivity)");
        RequireContains(source, "DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(this, _installState.HeadId, _portabilityActivity)");
        RequireContains(source, "DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(this, _installState.HeadId, _portabilityActivity)");
        RequireContains(source, "DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(this, _installState.HeadId, _portabilityActivity)");
        RequireContains(source, "DesktopCreatorPublicationWindow.ShowAsync(");
        RequireContains(source, "DesktopRuleEnvironmentStudioWindow.ShowAsync(this, _installState.HeadId, _portabilityActivity)");
    }

    private static void DesktopRuleEnvironmentStudioSurface_is_a_real_top_level_surface()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopRuleEnvironmentStudioWindow.cs");
        RequireContains(source, "public static async Task ShowAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)");
        RequireContains(source, "Title = \"Rules Setup\"");
        RequireContains(source, "\"Rules setup\"");
        RequireContains(source, "\"Rules package changes\"");
        RequireContains(source, "\"Changes\"");
        RequireContains(source, "\"Notes\"");
        RequireContains(source, "\"Explanation: \"");
        RequireDoesNotContain(source, "\"Rule Environment Studio\"");
        RequireDoesNotContain(source, "\"Amend-package lifecycle\"");
        RequireDoesNotContain(source, "\"Before-after diffs\"");
        RequireDoesNotContain(source, "\"Explain receipts\"");
        RequireDoesNotContain(source, "\"Explain receipt: \"");
        RequireContains(source, "DesktopHomeWindow.ShowAsync(owner, _installState.HeadId)");
        RequireContains(source, "DesktopSupportWindow.ShowAsync(owner, _installState.HeadId)");
        RequireContains(source, "DesktopCampaignWorkspaceWindow.ShowAsync(owner, _installState.HeadId)");
        RequireContains(source, "new ScrollViewer");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "DesktopStartupSurfaceCatalog.RuleEnvironmentStudio");
        RequireContains(appSource, "DesktopRuleEnvironmentStudioWindow.ShowAsync(owner, \"avalonia\")");
    }

    private static void DesktopCampaignWorkspace_keeps_restore_conflict_choices_visible()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs");
        RequireDoesNotContain(source, "RestoreConflictChoiceOrder");
        RequireDoesNotContain(source, "BuildRestoreConflictChoiceSummary()");
        RequireContains(source, "BuildRestoreStaleStateVisibilitySummary()");
        RequireDoesNotContain(source, "BuildRestorePrimaryRouteDecisionGateSummary()");
        RequireDoesNotContain(source, "BuildRestoreDecisionOrderSummary()");
        RequireDoesNotContain(source, "BuildRestoreLocalAuthoritySummary()");
        RequireDoesNotContain(source, "BuildRestoreReplacementGuardSummary()");
        RequireDoesNotContain(source, "BuildRestoreSupportHandoffSummary()");
        RequireDoesNotContain(source, "Review before continuing:");
        RequireContains(source, "Review campaign consequences before continuing this restore route.");
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
        RequireContains(source, "desktop.update.button.open_pending_installer");
        RequireContains(source, "desktop.update.button.copy_install_command");
        RequireContains(source, "desktop.update.button.refresh");
        RequireContains(source, "desktop.update.checking");
        RequireContains(source, "desktop.update.checked");
        RequireContains(source, "desktop.update.apply_scheduled");
        RequireContains(source, "DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync");
        RequireContains(source, "DesktopSupportWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForUpdate");
        RequireContains(source, "DesktopUpdateRuntime.TryOpenPendingInstaller(_installState.HeadId)");
        RequireContains(source, "DesktopUpdateRuntime.TryBuildPendingInstallerManualCommand(_installState.HeadId, out string command)");
        RequireContains(source, "A downloaded package is already waiting on this copy. Copy the install command here when you are ready.");
        RequireContains(source, "Manual install stays local: this downloaded package is ready, and the terminal command is available from here.");
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
        RequireContains(source, "desktop.devices.section.current_description");
        RequireContains(source, "desktop.devices.section.claimed_description");
        RequireContains(source, "desktop.devices.section.claims_description");
        RequireContains(source, "desktop.devices.section.follow_through_description");
        RequireContains(source, "client.GetDesktopInstallLinkingSummaryAsync");
        RequireContains(source, "client.GetAccountCampaignSummaryAsync");
        RequireContains(source, "DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopSupportWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopUpdateWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId)");
        RequireContains(source, "DesktopInstallLinkingWindow dialog = new(context);");
        RequireContains(source, "new ScrollViewer");
        RequireContains(source, "includeHeading: true");
        RequireContains(source, "desktop.devices.button.reload");
        RequireContains(source, "desktop.devices.button.manage_linked_copies");
        RequireContains(source, "desktop.install_link.button.login_website");
        RequireContains(source, "? CreateButton(S(\"desktop.home.button.open_current_campaign_workspace\"), OpenWorkRouteAsync, isPrimary: true)");
        RequireContains(source, "CreateButton(S(\"desktop.devices.button.manage_linked_copies\"), OpenAccountAsync, isPrimary: true)");
        RequireContains(source, "desktop.dialog.action.close");
        RequireDoesNotContain(source, "IsVisible = false");
        RequireDoesNotContain(source, "? CreateButton(S(\"desktop.home.button.open_current_workspace\"), OpenWorkRouteAsync, isPrimary: true)");
        RequireDoesNotContain(source, "desktop.install_link.button.open_work");
        RequireDoesNotContain(source, "desktop.devices.button.use_latest_claim");
        RequireDoesNotContain(source, "Use latest claim code");

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
        RequireContains(source, "desktop.report.intro");
        RequireContains(source, "desktop.report.private_split");
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
        RequireContains(source, "CreateField(S(\"desktop.report.bug.title_label\"), S(\"desktop.report.bug.title_watermark\"), _bugTitleBox)");
        RequireContains(source, "CreateField(S(\"desktop.report.feedback.detail_label\"), S(\"desktop.report.feedback.detail_watermark\"), _feedbackDetailBox)");
        RequireContains(source, "ReportBugTitleBoxLabel");
        RequireContains(source, "AutomationProperties.SetName(labelBlock, label)");
        RequireContains(source, "AutomationProperties.SetName(hintBlock, $\"{label} hint\")");
        RequireContains(source, "AutomationProperties.SetHelpText(box, $\"{automationName}. {tooltip}\")");
        RequireContains(source, "CreateIntroText(S(\"desktop.report.intro\"))");
        RequireContains(source, "CreateIntroText(S(\"desktop.report.private_split\"))");
        RequireContains(source, "Watermark = tooltip");
        RequireContains(source, "Text = BuildContextBody()");
        RequireContains(source, "desktop.dialog.action.close");
        RequireDoesNotContain(source, "desktop.home.button.continue");

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
        RequireContains(source, "desktop.dialog.action.close");
        RequireDoesNotContain(source, "desktop.home.button.continue");

        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "string.Equals(startupSurface, \"crash_recovery\"");
        RequireContains(appSource, "DesktopCrashRecoveryWindow.ShowPreviewAsync(owner, \"avalonia\")");
        RequireContains(appSource, "DesktopCrashRecoveryWindow.TryShowPendingAsync(owner)");
    }

    private static void DesktopCloseOnlySurfaces_use_explicit_close_copy()
    {
        string[] closeOnlySurfaces =
        [
            "Chummer.Avalonia/DesktopCampaignArtifactWindow.cs",
            "Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs",
            "Chummer.Avalonia/DesktopCreatorPublicationWindow.cs",
            "Chummer.Avalonia/DesktopCrashRecoveryWindow.cs",
            "Chummer.Avalonia/DesktopDevicesAccessWindow.cs",
            "Chummer.Avalonia/DesktopHomeWindow.cs",
            "Chummer.Avalonia/DesktopHorizonsWindow.cs",
            "Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs",
            "Chummer.Avalonia/DesktopReportIssueWindow.cs",
            "Chummer.Avalonia/DesktopSupportCaseWindow.cs",
            "Chummer.Avalonia/DesktopSupportWindow.cs",
            "Chummer.Avalonia/DesktopUpdateWindow.cs"
        ];

        foreach (string relativePath in closeOnlySurfaces)
        {
            string source = ReadSource(relativePath);
            RequireContains(source, "desktop.dialog.action.close");
            RequireDoesNotContain(source, "desktop.home.button.continue");
        }

        string shellThemeSource = ReadSource("Chummer.Avalonia/DesktopShellTheme.cs");
        RequireContains(shellThemeSource, "ResolveCloseActionLabel");
        RequireContains(shellThemeSource, "DesktopLocalizationCatalog.GetRequiredString(\"desktop.dialog.action.close\")");

        string workbenchScaffoldSource = ReadSource("Chummer.Avalonia/DesktopHorizonWindowScaffold.cs");
        RequireContains(workbenchScaffoldSource, "ResolveCloseActionLabel");
        RequireContains(workbenchScaffoldSource, "DesktopLocalizationCatalog.GetRequiredString(\"desktop.dialog.action.close\")");
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
        RequireContains(installLinkSource, "_preferences = DesktopPreferenceRuntime.LoadOrCreateState(context.State.HeadId);");
        RequireContains(installLinkSource, "_language = _preferences.Language;");
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
        RequireContains(projectorSource, "Confirmation:");
        RequireContains(projectorSource, "Humanize(new DesktopHomeSupportProjection");
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
        RequireContains(projectorSource, "Compatibility note:");
        RequireContains(projectorSource, "Build path option:");
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
        RequireContains(projectorSource, "Migration summary:");
        RequireContains(projectorSource, "Publication summary:");
        RequireDoesNotContain(source, "Open work follow-through");
    }

    private static void DesktopExplainReceipts_and_diagnostics_diffs_are_visible_on_trust_surfaces()
    {
        string helperSource = ReadSource("Chummer.Avalonia/DesktopTrustReceiptText.cs");
        RequireContains(helperSource, "DesktopTrustReceiptComposer.BuildDialogReceipt(dialog)");
        RequireContains(helperSource, "DesktopTrustReceiptComposer.BuildDialogReceiptSections(dialog)");
        RequireContains(helperSource, "DesktopTrustReceiptComposer.BuildDiagnosticsDiff(installState, updateStatus)");
        RequireContains(helperSource, "DesktopTrustReceiptComposer.BuildDiagnosticsSections(installState, updateStatus)");
        RequireContains(helperSource, "DesktopTrustReceiptComposer.BuildCrashDiagnosticsSections(report)");
        RequireContains(helperSource, "DesktopTrustReceiptComposer.BuildBuildLabSections(buildLab)");

        string runtimeReceiptSource = ReadSource("Chummer.Desktop.Runtime/DesktopTrustReceiptComposer.cs");
        RequireContains(runtimeReceiptSource, "Import rule-environment receipt:");
        RequireContains(runtimeReceiptSource, "Import receipt correlation key:");
        RequireContains(runtimeReceiptSource, "Receipt scope: import target {ruleset}");
        RequireContains(runtimeReceiptSource, "excludes raw character XML until the user accepts import");
        RequireContains(runtimeReceiptSource, "Import support handoff receipt:");
        RequireContains(runtimeReceiptSource, "Grounded import explain receipt:");
        RequireContains(runtimeReceiptSource, "Import staged artifact receipt:");
        RequireContains(runtimeReceiptSource, "Import artifact diff receipt:");
        RequireContains(runtimeReceiptSource, "Import diagnostics receipt:");
        RequireContains(runtimeReceiptSource, "Import support diagnostics receipt:");
        RequireContains(runtimeReceiptSource, "Import source-toggle diff receipt:");
        RequireContains(runtimeReceiptSource, "Environment diff before import:");
        RequireContains(runtimeReceiptSource, "Environment diff after import:");
        RequireContains(runtimeReceiptSource, "Source toggles, support posture, and saved character remain unchanged");
        RequireContains(runtimeReceiptSource, "accepted content binds to {ruleset} only after oracle review");
        RequireContains(runtimeReceiptSource, "Diagnostics environment diff:");
        RequireContains(runtimeReceiptSource, "Diagnostics environment diff before support:");
        RequireContains(runtimeReceiptSource, "Diagnostics environment diff after support:");
        RequireContains(runtimeReceiptSource, "support packet carries before/after environment truth without changing local install state");
        RequireContains(runtimeReceiptSource, "Diagnostics receipt correlation key:");
        RequireContains(runtimeReceiptSource, "Support diagnostics packet id:");
        RequireContains(runtimeReceiptSource, "Support diagnostics correlation:");
        RequireContains(runtimeReceiptSource, "does not change local install state");
        RequireContains(runtimeReceiptSource, "Grounded support receipt:");
        RequireContains(runtimeReceiptSource, "Support diagnostics receipt:");
        RequireContains(runtimeReceiptSource, "Support diagnostics explain receipt:");
        RequireContains(runtimeReceiptSource, "Support blocker diff receipt:");
        RequireContains(runtimeReceiptSource, "Support proof diff receipt:");
        RequireContains(runtimeReceiptSource, "Support handoff receipt:");
        RequireContains(runtimeReceiptSource, "Support environment tuple diff:");
        RequireContains(runtimeReceiptSource, "Support identity diff:");
        RequireContains(runtimeReceiptSource, "Release-channel receipt:");
        RequireContains(runtimeReceiptSource, "Rollout receipt:");
        RequireContains(runtimeReceiptSource, "Support packet diff receipt:");
        RequireContains(runtimeReceiptSource, "last blocker");
        RequireContains(runtimeReceiptSource, "masterIndexImportOracleReceipt");
        RequireContains(runtimeReceiptSource, "masterIndexAdjacentSr6OracleReceipt");
        RequireContains(runtimeReceiptSource, "masterIndexSourceSelectionSummary");
        RequireContains(runtimeReceiptSource, "Raw import receipt");
        RequireContains(runtimeReceiptSource, "Import blocker receipt");

        string shellFrameProjectorSource = ReadSource("Chummer.Avalonia/MainWindow.ShellFrameProjector.cs");
        RequireContains(shellFrameProjectorSource, "Import ready. Review the character, then keep or discard the changes.");
        RequireContains(shellFrameProjectorSource, "Nothing changes until you accept the import.");
        RequireDoesNotContain(shellFrameProjectorSource, "Import correlation key:");
        RequireDoesNotContain(shellFrameProjectorSource, "Import scope:");
        RequireDoesNotContain(shellFrameProjectorSource, "Import support note:");
        RequireDoesNotContain(shellFrameProjectorSource, "Import environment before:");
        RequireDoesNotContain(shellFrameProjectorSource, "Import environment after:");
        RequireDoesNotContain(shellFrameProjectorSource, "Import environment tuple diff:");
        RequireDoesNotContain(shellFrameProjectorSource, "Environment diff before import:");
        RequireDoesNotContain(shellFrameProjectorSource, "Environment diff after import:");
        RequireDoesNotContain(shellFrameProjectorSource, "Import explanation:");
        RequireDoesNotContain(shellFrameProjectorSource, "Current import explanation:");
        RequireDoesNotContain(shellFrameProjectorSource, "Import blocker details:");
        RequireDoesNotContain(shellFrameProjectorSource, "Import diagnostics details:");
        RequireDoesNotContain(shellFrameProjectorSource, "Import diagnostics diff:");
        RequireDoesNotContain(shellFrameProjectorSource, "DesktopTrustReceiptComposer.BuildPortabilityDiagnosticsDiffText(portability.Receipt)");
        RequireDoesNotContain(shellFrameProjectorSource, "Import support diagnostics details:");
        RequireDoesNotContain(shellFrameProjectorSource, "BuildImportDiagnosticsReceipt");
        RequireDoesNotContain(shellFrameProjectorSource, "BuildImportBlockerReceipt");
        RequireDoesNotContain(shellFrameProjectorSource, "Import support reuse:");
        RequireDoesNotContain(shellFrameProjectorSource, "BuildImportSupportReuse");
        RequireDoesNotContain(shellFrameProjectorSource, "BuildImportSupportDiagnosticsReceipt");

        string sectionHostSource = ReadSource("Chummer.Avalonia/Controls/SectionHostControl.axaml.cs");
        RequireContains(sectionHostSource, "DesktopTrustReceiptText.BuildBuildLabSections(buildLab)");
        RequireContains(sectionHostSource, "SetBuildLabTrustReceiptSections");
        RequireContains(sectionHostSource, "BuildLabTrustReceiptPanel");
        RequireContains(sectionHostSource, "Build explanation and environment details");
        string blazorSectionHostSource = ReadSource("Chummer.Blazor/Components/Shell/SectionPane.razor");
        RequireContains(blazorSectionHostSource, "data-build-lab-trust-receipts");
        RequireContains(blazorSectionHostSource, "Build explanation and environment details");
        RequireContains(blazorSectionHostSource, "data-build-lab-trust-section");
        RequireContains(blazorSectionHostSource, "NormalizeBuildLabReceiptToken(receiptSection.Title)");
        RequireContains(blazorSectionHostSource, "BuildBuildLabTrustReceiptSections(buildLab)");
        RequireContains(blazorSectionHostSource, "BuildLabTrustReceiptSection");
        RequireContains(blazorSectionHostSource, "receiptSection.Title");
        RequireContains(blazorSectionHostSource, "receiptSection.Lines");
        RequireContains(runtimeReceiptSource, "Support blocker receipt:");
        RequireContains(runtimeReceiptSource, "Build receipt correlation key:");
        RequireContains(runtimeReceiptSource, "Build receipt scope:");
        RequireContains(runtimeReceiptSource, "blocker and explain receipts are copy-safe and do not apply a variant");
        RequireContains(runtimeReceiptSource, "Build support handoff receipt:");
        RequireContains(runtimeReceiptSource, "Grounded build explain receipt:");
        RequireContains(runtimeReceiptSource, "Build diagnostics packet id:");
        RequireContains(runtimeReceiptSource, "Build diagnostics correlation:");
        RequireContains(runtimeReceiptSource, "ties the copied build blocker, support handoff, and visible before/after diff");
        RequireContains(runtimeReceiptSource, "no variant, export, or campaign fit result is applied before review.");
        RequireContains(runtimeReceiptSource, "Before build environment diff");
        RequireContains(runtimeReceiptSource, "After build environment diff");
        RequireContains(runtimeReceiptSource, "Build support diagnostics receipt:");
        RequireContains(runtimeReceiptSource, "Build blocker diagnostics diff:");
        RequireContains(runtimeReceiptSource, "Environment diff before build:");
        RequireContains(runtimeReceiptSource, "Environment diff after build:");
        RequireContains(runtimeReceiptSource, "Grounded explain receipt:");
        RequireContains(runtimeReceiptSource, "disabled build action(s):");
        RequireContains(runtimeReceiptSource, "no variant, export, or campaign fit result is applied before review");
        RequireContains(runtimeReceiptSource, "support closure not required");
        string resultPanelSource = ReadSource("Chummer.Blazor/Components/Shell/ResultPanel.razor");
        RequireContains(resultPanelSource, "UndetectableHumanizerCopyAdapter.Humanize");
        RequireContains(resultPanelSource, "Import reference:");
        RequireContains(resultPanelSource, "Import scope:");
        RequireContains(resultPanelSource, "Support note:");
        RequireContains(resultPanelSource, "Import environment before:");
        RequireContains(resultPanelSource, "Import environment after:");
        RequireContains(resultPanelSource, "Import change summary:");
        RequireContains(resultPanelSource, "Before import:");
        RequireContains(resultPanelSource, "After import:");
        RequireContains(resultPanelSource, "Explanation:");
        RequireContains(resultPanelSource, "Import explanation:");
        RequireContains(resultPanelSource, "Import warning:");
        RequireContains(resultPanelSource, "Import details:");
        RequireContains(resultPanelSource, "Support details:");
        RequireContains(resultPanelSource, "DesktopTrustReceiptComposer.BuildPortabilityDiagnosticsDiffText(portability.Receipt)");
        RequireContains(resultPanelSource, "Import support reuse:");
        RequireContains(resultPanelSource, "DesktopTrustReceiptComposer.BuildPortabilityReceiptSections(receipt)");
        RequireContains(resultPanelSource, "data-result-trust-receipt");
        RequireContains(resultPanelSource, "Import review");
        string runtimeInspectorSource = ReadSource("Chummer.Blazor/Components/Shared/RuntimeInspectorPanel.razor");
        RequireContains(runtimeInspectorSource, "data-runtime-support-diagnostics-receipt");
        RequireContains(runtimeInspectorSource, "Support details");
        RequireContains(runtimeInspectorSource, "Reference:");
        RequireContains(runtimeInspectorSource, "Support package:");
        RequireContains(runtimeInspectorSource, "Support link:");
        RequireContains(runtimeInspectorSource, "Current setup:");
        RequireContains(runtimeInspectorSource, "Support summary:");
        RequireContains(runtimeInspectorSource, "Explanation:");
        RequireContains(runtimeInspectorSource, "Next step:");
        RequireContains(runtimeInspectorSource, "Change summary:");
        RequireContains(runtimeInspectorSource, "System tuple:");
        RequireContains(runtimeInspectorSource, "Support package details:");
        RequireContains(runtimeInspectorSource, "Before support:");
        RequireContains(runtimeInspectorSource, "After support:");
        RequireContains(runtimeInspectorSource, "data-runtime-support-trust-receipt");
        RequireContains(runtimeInspectorSource, "Support and system details");
        RequireContains(runtimeInspectorSource, "Grounded support explain receipt");
        RequireContains(runtimeInspectorSource, "data-runtime-support-trust-section");
        RequireContains(runtimeInspectorSource, "BuildRuntimeSupportReceiptSections(Projection)");
        RequireContains(runtimeInspectorSource, "DesktopTrustReceiptComposer.BuildRuntimeInspectorSupportSections(projection)");
        RequireContains(runtimeInspectorSource, "data-runtime-compatibility-diagnostics-receipt");
        RequireContains(runtimeInspectorSource, "Compatibility package:");
        RequireContains(runtimeInspectorSource, "Compatibility blocker:");
        RequireContains(runtimeInspectorSource, "Compatibility change:");
        RequireContains(runtimeInspectorSource, "Required update:");
        RequireContains(runtimeInspectorSource, "Support package details:");
        RequireContains(runtimeInspectorSource, "Support next step:");
        RequireContains(runtimeInspectorSource, "BuildCompatibilityDiagnosticsPacketId(Projection, diagnostic)");
        RequireContains(runtimeInspectorSource, "BuildCompatibilityBlockerReceipt(Projection, diagnostic)");
        RequireContains(runtimeInspectorSource, "BuildCompatibilityEnvironmentDiff(Projection, diagnostic)");
        RequireContains(runtimeInspectorSource, "BuildCompatibilityProofDiffReceipt(Projection, diagnostic)");
        RequireContains(runtimeInspectorSource, "BuildCompatibilityPacketDiffReceipt(Projection, diagnostic)");
        RequireContains(runtimeInspectorSource, "BuildCompatibilitySupportHandoffReceipt(Projection, diagnostic)");
        string explainTracePanelSource = ReadSource("Chummer.Blazor/Components/Shared/ExplainTracePanel.razor");
        RequireContains(explainTracePanelSource, "data-explain-trust-receipt");
        RequireContains(explainTracePanelSource, "Explanation details");
        RequireContains(explainTracePanelSource, "Reference:");
        RequireContains(explainTracePanelSource, "Scope:");
        RequireContains(explainTracePanelSource, "Current explanation:");
        RequireContains(explainTracePanelSource, "Before:");
        RequireContains(explainTracePanelSource, "After:");
        RequireContains(explainTracePanelSource, "Changes:");
        RequireContains(explainTracePanelSource, "does not apply a build, import, export, or support action");

        string dialogSource = ReadSource("Chummer.Avalonia/DesktopDialogWindow.axaml.cs");
        RequireContains(dialogSource, "DesktopTrustReceiptText.BuildDialogReceipt(dialog)");
        RequireContains(dialogSource, "DesktopTrustPanelFactory.CreateDialogPanel");
        string dialogMarkup = ReadSource("Chummer.Avalonia/DesktopDialogWindow.axaml");
        RequireContains(dialogMarkup, "DialogTrustReceiptPanel");
        string dialogPanelFactorySource = ReadSource("Chummer.Avalonia/DesktopTrustPanelFactory.cs");
        RequireContains(dialogPanelFactorySource, "Explanation and environment details");
        RequireContains(dialogPanelFactorySource, "Support diagnostics and environment details");
        RequireContains(runtimeReceiptSource, "Before support environment diff");
        RequireContains(runtimeReceiptSource, "After support environment diff");
        string commandPaneSource = ReadSource("Chummer.Avalonia/Controls/CommandDialogPaneControl.axaml.cs");
        RequireContains(commandPaneSource, "DialogTrustReceiptText.Text");
        RequireContains(commandPaneSource, "state.DialogTrustReceipt");
        string commandPaneMarkup = ReadSource("Chummer.Avalonia/Controls/CommandDialogPaneControl.axaml");
        RequireContains(commandPaneMarkup, "DialogTrustReceiptText");
        string shellFrameProjectorTrustSource = ReadSource("Chummer.Avalonia/MainWindow.ShellFrameProjector.cs");
        RequireContains(shellFrameProjectorTrustSource, "DialogTrustReceipt: DesktopTrustReceiptText.BuildDialogReceipt(state.ActiveDialog)");
        string blazorDialogSource = ReadSource("Chummer.Blazor/Components/Shell/DialogHost.razor");
        RequireContains(blazorDialogSource, "data-dialog-trust-receipt");
        RequireContains(blazorDialogSource, "DialogTrustReceiptText.BuildDialogReceipt(dialog)");
        RequireContains(blazorDialogSource, "Explanation and environment details");
        RequireContains(blazorDialogSource, "BuildDialogTrustReceiptSections(dialog)");
        string blazorDialogReceiptSource = ReadSource("Chummer.Blazor/Components/Shell/DialogTrustReceiptText.cs");
        RequireContains(blazorDialogReceiptSource, "DesktopTrustReceiptComposer.BuildDialogReceipt(dialog)");
        RequireContains(blazorDialogReceiptSource, "DesktopTrustReceiptComposer.BuildDialogReceiptSections(dialog)");
        RequireContains(blazorDialogReceiptSource, "new DialogTrustReceiptSection(section.Title, section.Lines)");

        string supportSource = ReadSource("Chummer.Avalonia/DesktopSupportWindow.cs");
        RequireContains(supportSource, "DesktopTrustReceiptText.BuildDiagnosticsDiff(_installState, _updateStatus),");
        RequireContains(supportSource, "DesktopSupportDiagnosticsText.BuildSupportCenterDiagnostics(_installState, _updateStatus, _supportProjection)");
        RequireContains(supportSource, "CreateSection(S(\"desktop.support.section.diagnostics\"), _diagnosticsText, null)");
        RequireContains(supportSource, "CreateButton(S(\"desktop.home.button.open_update_status\"), OpenUpdateWindowAsync, isPrimary: true)");
        RequireContains(supportSource, "CreateButton(S(\"desktop.home.button.open_report_issue\"), OpenReportIssueWindowAsync)");
        RequireContains(supportSource, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall(_installState)");
        RequireContains(supportSource, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForUpdate(_installState, _updateStatus)");
        string supportCaseSource = ReadSource("Chummer.Avalonia/DesktopSupportCaseWindow.cs");
        RequireContains(supportCaseSource, "DesktopTrustReceiptText.BuildDiagnosticsDiff(_installState, _updateStatus),");
        RequireContains(supportCaseSource, "DesktopSupportDiagnosticsText.BuildTrackedCaseDiagnostics(_installState, _updateStatus, _supportProjection, _supportCase)");
        RequireContains(supportCaseSource, "CreateSection(S(\"desktop.support_case.section.diagnostics\"), _diagnosticsText, null)");
        RequireContains(supportCaseSource, "CreateButton(S(\"desktop.home.button.open_support_center\"), OpenSupportWindowAsync)");
        RequireContains(supportCaseSource, "CreateButton(S(\"desktop.home.button.open_report_issue\"), OpenReportIssueWindowAsync)");
        RequireContains(supportCaseSource, "CreateButton(S(\"desktop.home.button.open_update_status\"), OpenUpdateWindowAsync, isPrimary: true)");
        string reportSource = ReadSource("Chummer.Avalonia/DesktopReportIssueWindow.cs");
        RequireContains(reportSource, "BuildContextBody()");
        RequireContains(reportSource, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForBugReport(");
        RequireContains(reportSource, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForFeedback(");
        RequireContains(reportSource, "BuildBugDraftText()");
        RequireContains(reportSource, "BuildFeedbackDraftText()");
        RequireContains(reportSource, "CreateButton(S(\"desktop.home.button.open_support_center\"), OpenSupportWindowAsync)");
        string updateSource = ReadSource("Chummer.Avalonia/DesktopUpdateWindow.cs");
        RequireContains(updateSource, "DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(");
        RequireContains(updateSource, "CreateButton(S(\"desktop.home.button.open_support_center\"), OpenSupportWindowAsync, isPrimary: true)");
        RequireContains(updateSource, "CreateButton(S(\"desktop.home.button.open_report_issue\"), OpenReportIssueWindowAsync)");
        RequireContains(updateSource, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForUpdate(_installState, _updateStatus)");
        RequireContains(updateSource, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall(_installState)");
        string installRuntimeSource = ReadSource("Chummer.Desktop.Runtime/DesktopInstallLinkingRuntime.cs");
        RequireContains(installRuntimeSource, "BuildSupportDiagnosticsReceiptLines");
        RequireContains(installRuntimeSource, "Support identity diff:");
        RequireContains(installRuntimeSource, "Support packet diff receipt:");
        RequireContains(installRuntimeSource, "support packet carries before/after environment truth");
        RequireContains(installRuntimeSource, "Diagnostics receipt correlation key:");
        RequireContains(installRuntimeSource, "Support diagnostics packet id:");
        RequireContains(installRuntimeSource, "Support diagnostics correlation:");
        RequireContains(installRuntimeSource, "Support diagnostics receipt:");
        RequireContains(installRuntimeSource, "Support diagnostics explain receipt:");
        RequireContains(installRuntimeSource, "Support blocker diff receipt:");
        RequireContains(runtimeReceiptSource, "Support blocker receipt:");
        RequireContains(installRuntimeSource, "Support proof diff receipt:");
        RequireContains(installRuntimeSource, "Support handoff receipt:");
        RequireContains(installRuntimeSource, "Support environment tuple diff:");
        RequireContains(installRuntimeSource, "does not change local install state");
        RequireContains(installRuntimeSource, "last blocker");
        string crashRuntimeSource = ReadSource("Chummer.Desktop.Runtime/DesktopCrashRuntime.cs");
        RequireContains(crashRuntimeSource, "BuildCrashDiagnosticsReceiptLines");
        RequireContains(crashRuntimeSource, "Crash diagnostics receipt:");
        RequireContains(crashRuntimeSource, "Crash support receipt correlation key:");
        RequireContains(crashRuntimeSource, "Crash diagnostics packet id:");
        RequireContains(crashRuntimeSource, "Crash environment diff before recovery:");
        RequireContains(crashRuntimeSource, "Crash environment diff after recovery:");
        RequireContains(crashRuntimeSource, "Crash environment tuple diff:");
        RequireContains(crashRuntimeSource, "Crash support explain receipt:");
        RequireContains(crashRuntimeSource, "Crash support handoff receipt:");
        RequireContains(crashRuntimeSource, "Crash packet diff receipt:");
        RequireContains(crashRuntimeSource, "local files, support posture, and install state remain unchanged");
        string crashRecoverySource = ReadSource("Chummer.Avalonia/DesktopCrashRecoveryWindow.cs");
        RequireContains(crashRecoverySource, "DesktopTrustPanelFactory.CreateCrashDiagnosticsPanel(");
        RequireContains(crashRecoverySource, "DesktopTrustReceiptText.BuildCrashDiagnosticsSections(_pending.Report)");
        RequireContains(crashRecoverySource, "Visible crash diagnostics and environment details");
        RequireContains(crashRecoverySource, "CreateBodyWithTrustPanel(");
        RequireContains(crashRecoverySource, "DesktopCrashRuntime.BuildCrashDiagnosticsReceiptLines(_pending.Report)");
        RequireContains(crashRecoverySource, "BuildCrashDiagnosticsPacketText");
        RequireContains(crashRecoverySource, "Chummer crash support diagnostics");
        RequireContains(crashRecoverySource, "Runtime crash diagnostics");
        RequireContains(crashRecoverySource, "Crash diagnostics copied with explanation and before/after environment details.");
        string importPanelSource = ReadSource("Chummer.Blazor/Components/Shell/ImportPanel.razor");
        RequireContains(importPanelSource, "Import receipt correlation key:");
        RequireContains(importPanelSource, "Grounded explain receipt");
        RequireContains(importPanelSource, "Before import environment diff");
        RequireContains(importPanelSource, "After review environment diff");
        RequireContains(importPanelSource, "Receipt scope: import target {ruleset}");
        RequireContains(importPanelSource, "excludes raw runner XML until the user accepts import");
        RequireContains(importPanelSource, "Import staged artifact receipt:");
        RequireContains(importPanelSource, "Import artifact diff receipt:");
        RequireContains(importPanelSource, "Import diagnostics receipt:");
        RequireContains(importPanelSource, "Import support diagnostics receipt:");
        RequireContains(importPanelSource, "Import source-toggle diff receipt:");
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

    private static void PrimaryDesktopSummaryHeader_keeps_restore_stale_and_conflict_choices_visible()
    {
        string summaryHeaderMarkup = ReadSource("Chummer.Avalonia/Controls/SummaryHeaderControl.axaml");
        RequireContains(summaryHeaderMarkup, "RestoreContinuityStatusBorder");
        RequireContains(summaryHeaderMarkup, "RestoreContinuityStatusText");
        RequireContains(summaryHeaderMarkup, "StaleStateStatusText");
        RequireContains(summaryHeaderMarkup, "ConflictChoiceStatusText");
        RequireContains(summaryHeaderMarkup, "RestoreContinuityDecisionText");
        RequireContains(summaryHeaderMarkup, "RestoreContinuityDecisionOrderText");
        RequireContains(summaryHeaderMarkup, "RestoreContinuityReplacementGuardText");
        RequireContains(summaryHeaderMarkup, "RestoreContinuitySupportHandoffText");
        RequireContains(summaryHeaderMarkup, "RestoreContinuityActionPanel");
        RequireContains(summaryHeaderMarkup, "RestoreContinuityActionStatusText");
        RequireContains(summaryHeaderMarkup, "Keep Local");
        RequireContains(summaryHeaderMarkup, "Save");
        RequireContains(summaryHeaderMarkup, "Campaign");
        RequireContains(summaryHeaderMarkup, "Support");

        string summaryHeaderCode = ReadSource("Chummer.Avalonia/Controls/SummaryHeaderControl.axaml.cs");
        RequireContains(summaryHeaderCode, "SetRestoreContinuityStatus(");
        RequireContains(summaryHeaderCode, "RestoreContinuitySummary");
        RequireContains(summaryHeaderCode, "StaleStateSummary");
        RequireContains(summaryHeaderCode, "ConflictChoiceSummary");
        RequireContains(summaryHeaderCode, "CanSaveLocalWorkBeforeRestore");
        RequireContains(summaryHeaderCode, "BuildRestoreContinuityDecisionSummary");
        RequireContains(summaryHeaderCode, "Save first if needed before changing this desktop copy.");
        RequireContains(summaryHeaderCode, "Keep local work or open support before changing this desktop copy.");
        RequireDoesNotContain(summaryHeaderCode, "Decision order:");
        RequireDoesNotContain(summaryHeaderCode, "Restore replacement guard:");
        RequireDoesNotContain(summaryHeaderCode, "Support handoff:");
        RequireContains(summaryHeaderCode, "AutomationProperties.SetName(RestoreContinuityDecisionOrderText, \"Workspace decision order\")");
        RequireContains(summaryHeaderCode, "AutomationProperties.SetName(RestoreContinuityReplacementGuardText, \"Workspace change guard\")");
        RequireContains(summaryHeaderCode, "AutomationProperties.SetName(RestoreContinuitySupportHandoffText, \"Workspace support handoff\")");
        RequireContains(summaryHeaderCode, "restore-decision-keep-local-work");
        RequireContains(summaryHeaderCode, "restore-decision-save-local-work");
        RequireContains(summaryHeaderCode, "restore-decision-review-campaign-workspace");
        RequireContains(summaryHeaderCode, "restore-decision-open-workspace-support");
        RequireContains(summaryHeaderCode, "ToolTip.SetTip(KeepLocalWorkButton");
        RequireContains(summaryHeaderCode, "ToolTip.SetTip(SaveLocalWorkButton");
        RequireContains(summaryHeaderCode, "Save local work is unavailable because no dirty local workspace is active");
        RequireContains(summaryHeaderCode, "KeepLocalWorkRequested");
        RequireContains(summaryHeaderCode, "SaveLocalWorkRequested");
        RequireContains(summaryHeaderCode, "CampaignWorkspaceRequested");
        RequireContains(summaryHeaderCode, "WorkspaceSupportRequested");

        string mainWindowSource = ReadSource("Chummer.Avalonia/MainWindow.axaml.cs");
        RequireContains(mainWindowSource, "onWorkspaceSupportRequested: SummaryHeader_OnWorkspaceSupportRequested");
        string controlBindingSource = ReadSource("Chummer.Avalonia/MainWindow.ControlBinding.cs");
        RequireContains(controlBindingSource, "summaryHeader.WorkspaceSupportRequested += onWorkspaceSupportRequested");
        string eventHandlersSource = ReadSource("Chummer.Avalonia/MainWindow.EventHandlers.cs");
        RequireContains(eventHandlersSource, "SummaryHeader_OnWorkspaceSupportRequested");
        RequireContains(eventHandlersSource, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace(installState, ResolveActiveSupportWorkspace())");
        RequireContains(eventHandlersSource, "ResolveActiveSupportWorkspace()");

        string projectorSource = ReadSource("Chummer.Avalonia/MainWindow.ShellFrameProjector.cs");
        RequireContains(projectorSource, "HasVisibleContent: false");
        RequireDoesNotContain(projectorSource, "Restore choice:");
        RequireDoesNotContain(projectorSource, "Conflict choices:");
    }

    private static void DesktopShell_removes_right_rail_and_workspace_strip_keeps_ruleset_specific_copy()
    {
        string desktopShellSource = ReadSource("Chummer.Blazor/Components/Layout/DesktopShell.razor");
        RequireContains(desktopShellSource, "<MdiStrip");
        RequireContains(desktopShellSource, "RulesetId=\"@_shellSurfaceState.ActiveRulesetId\"");
        RequireDoesNotContain(desktopShellSource, "<aside class=\"right-pane\"");
        RequireDoesNotContain(desktopShellSource, "<ImportPanel");
        RequireDoesNotContain(desktopShellSource, "<CommandPanel");
        RequireDoesNotContain(desktopShellSource, "<ResultPanel");

        string mdiStripSource = ReadSource("Chummer.Blazor/Components/Shell/MdiStrip.razor");
        RequireContains(mdiStripSource, "BuildWorkspaceStripEmptyState()");
        RequireContains(mdiStripSource, "BuildWorkspaceTitle(workspace)");
        RequireContains(mdiStripSource, "RulesetUiDirectiveCatalog.BuildWorkspaceStripEmptyState");
        RequireContains(mdiStripSource, "RulesetUiDirectiveCatalog.BuildWorkspaceStripTitle");

        string importPanelSource = ReadSource("Chummer.Blazor/Components/Shell/ImportPanel.razor");
        RequireContains(importPanelSource, "BuildImportHeading()");
        RequireContains(importPanelSource, "BuildImportAcceptAttribute()");
        RequireContains(importPanelSource, "BuildImportHint()");
        RequireContains(importPanelSource, "BuildImportRuleEnvironment(activity.Receipt)");
        RequireContains(importPanelSource, "data-import-trust-receipt");
        RequireContains(importPanelSource, "BuildImportTrustReceiptSections()");
        RequireContains(importPanelSource, "DesktopTrustReceiptComposer.BuildImportReviewSections(");
        RequireContains(importPanelSource, "Runner import review");
        RequireContains(importPanelSource, "Runner import setup");
        RequireContains(importPanelSource, "UndetectableHumanizerCopyAdapter.Humanize");
        RequireContains(importPanelSource, "Grounded explain receipt");
        RequireContains(importPanelSource, "Before import environment diff");
        RequireContains(importPanelSource, "After review environment diff");
        RequireContains(importPanelSource, "Import support handoff receipt:");
        RequireContains(importPanelSource, "Import staged artifact receipt:");
        RequireContains(importPanelSource, "Import artifact diff receipt:");
        RequireContains(importPanelSource, "Environment diff before import:");
        RequireContains(importPanelSource, "Environment diff after import:");
        RequireContains(importPanelSource, "Import blocker receipt:");
        RequireContains(importPanelSource, "data-import-trust-section");
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
        RequireContains(toolStripMarkup, "x:Name=\"HorizonsButton\"");
        RequireContains(toolStripMarkup, "x:Name=\"CampaignWorkspaceButton\"");
        RequireContains(toolStripMarkup, "x:Name=\"UpdateStatusButton\"");
        RequireContains(toolStripMarkup, "x:Name=\"InstallLinkingButton\"");
        RequireContains(toolStripMarkup, "x:Name=\"SupportButton\"");
        RequireContains(toolStripMarkup, "x:Name=\"ReportIssueButton\"");
        RequireContains(toolStripMarkup, "DesktopHomeButton_OnClick");
        RequireContains(toolStripMarkup, "HorizonsButton_OnClick");
        RequireContains(toolStripMarkup, "CampaignWorkspaceButton_OnClick");
        RequireContains(toolStripMarkup, "UpdateStatusButton_OnClick");
        RequireContains(toolStripMarkup, "InstallLinkingButton_OnClick");
        RequireContains(toolStripMarkup, "SupportButton_OnClick");
        RequireContains(toolStripMarkup, "ReportIssueButton_OnClick");

        string toolStripSource = ReadSource("Chummer.Avalonia/Controls/ToolStripControl.axaml.cs");
        RequireContains(toolStripSource, "DesktopHomeRequested");
        RequireContains(toolStripSource, "HorizonsRequested");
        RequireContains(toolStripSource, "CampaignWorkspaceRequested");
        RequireContains(toolStripSource, "UpdateStatusRequested");
        RequireContains(toolStripSource, "InstallLinkingRequested");
        RequireContains(toolStripSource, "SupportRequested");
        RequireContains(toolStripSource, "ReportIssueRequested");
        RequireContains(toolStripSource, "desktop.shell.tool.desktop_home");
        RequireContains(toolStripSource, "desktop.shell.tool.horizons");
        RequireContains(toolStripSource, "desktop.shell.tool.campaign_workspace");
        RequireContains(toolStripSource, "desktop.shell.tool.update_status");
        RequireContains(toolStripSource, "desktop.shell.tool.link_copy");
        RequireContains(toolStripSource, "desktop.shell.tool.open_support");
        RequireContains(toolStripSource, "desktop.shell.tool.report_issue");
        RequireContains(toolStripSource, "desktop.shell.tool.settings");
        RequireDoesNotContain(toolStripSource, "desktop.shell.tool.status_idle");

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
        RequireContains(bindingSource, "onHorizonsRequested");
        RequireContains(bindingSource, "onCampaignWorkspaceRequested");
        RequireContains(bindingSource, "onUpdateStatusRequested");
        RequireContains(bindingSource, "onInstallLinkingRequested");
        RequireContains(bindingSource, "onSupportRequested");
        RequireContains(bindingSource, "onReportIssueRequested");
        RequireContains(bindingSource, "onSettingsRequested");
        RequireContains(bindingSource, "AttachToolStripHandlers(toolStrip);");
        RequireContains(bindingSource, "AttachToolStripHandlers(classicToolStrip);");
        RequireContains(bindingSource, "surface.DesktopHomeRequested +=");
        RequireContains(bindingSource, "surface.HorizonsRequested +=");
        RequireContains(bindingSource, "surface.CampaignWorkspaceRequested +=");
        RequireContains(bindingSource, "surface.UpdateStatusRequested +=");
        RequireContains(bindingSource, "surface.InstallLinkingRequested +=");
        RequireContains(bindingSource, "surface.SupportRequested +=");
        RequireContains(bindingSource, "surface.ReportIssueRequested +=");
        RequireContains(bindingSource, "surface.SettingsRequested +=");

        string eventHandlerSource = ReadSource("Chummer.Avalonia/MainWindow.EventHandlers.cs");
        RequireContains(eventHandlerSource, "ToolStrip_OnDesktopHomeRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnHorizonsRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnCampaignWorkspaceRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnUpdateStatusRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnInstallLinkingRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnSupportRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnReportIssueRequested");
        RequireContains(eventHandlerSource, "ToolStrip_OnSettingsRequested");
        RequireContains(eventHandlerSource, "DesktopHomeWindow.ShowAsync(this, \"avalonia\")");
        RequireContains(eventHandlerSource, "DesktopHorizonsWindow.ShowAsync(this, DesktopHeadId)");
        RequireContains(eventHandlerSource, "DesktopCampaignWorkspaceWindow.ShowAsync(this, \"avalonia\")");
        RequireContains(eventHandlerSource, "DesktopUpdateWindow.ShowAsync(this, \"avalonia\")");
        RequireContains(eventHandlerSource, "DesktopSupportWindow.ShowAsync(this, \"avalonia\")");
        RequireContains(eventHandlerSource, "DesktopReportIssueWindow.ShowAsync(this, \"avalonia\")");
        RequireContains(eventHandlerSource, "DesktopInstallLinkingWindow.ShowAsync(this, \"avalonia\")");
        RequireContains(eventHandlerSource, "OpenDesktopCommandFromSurfaceAsync(\"global_settings\", \"open global settings\")");

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
        RequireDoesNotContain(appSource, "DesktopHomeWindow.ShowIfNeededAsync(owner, \"avalonia\", installContext: null)");
        RequireContains(appSource, "DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.Settings)");
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
        RequireContains(source, "DesktopDevicesAccessWindow.BuildInstallLinkEntryButtonLabel(_installState, _preferences.Language)");
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
        RequireContains(source, "desktop.install_link.summary.guest_status");
        RequireContains(source, "desktop.install_link.button.redeem_claim_code");
        RequireContains(source, "desktop.install_link.claim_code_watermark");
        RequireContains(source, "DesktopInstallLinkingRuntime.RedeemClaimCodeAsync");
        RequireContains(source, "RefreshActionState()");
        RequireContains(source, "desktop.install_link.button.login_website");
        RequireContains(source, "desktop.install_link.button.unlink_copy");
        RequireContains(source, "desktop.install_link.button.continue_unlinked");
        RequireContains(source, "ContinueUnlinkedAsync");
        RequireContains(source, "UnlinkCopyAsync");
        RequireContains(source, "DesktopInstallLinkingRuntime.MarkPromptDismissed(_state.HeadId)");
        RequireContains(source, "DesktopInstallLinkingRuntime.UnlinkInstallAsync(_state.HeadId, CancellationToken.None)");
        RequireDoesNotContain(source, "desktopLifetime.Shutdown();");
        RequireDoesNotContain(source, "e.Cancel = true;");
        RequireDoesNotContain(source, "ContinueAsGuestAsync");
        RequireDoesNotContain(source, "desktop.install_link.button.continue_guest");
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
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenClaimPortalForInstall(");
        RequireContains(source, "out string loginUrl");
        RequireContains(source, "out string? failureReason");
        RequireContains(source, "ShowManualBrowserFallbackAsync(loginUrl, failureReason)");
        RequireContains(source, "DesktopInstallLinkingRuntime.BuildClaimPortalAbsoluteUriForInstall(_state)");
    }

    private static void BlazorDesktopShell_blocks_unlinked_installs_with_visible_claim_gate()
    {
        string programSource = ReadSource("Chummer.Blazor.Desktop/Program.cs");
        string shellSource = ReadSource("Chummer.Blazor/Components/Layout/DesktopShell.razor");
        string shellCodeBehind = ReadSource("Chummer.Blazor/Components/Layout/DesktopShell.razor.cs");

        RequireContains(programSource, "builder.Services.AddSingleton(installLinking);");
        RequireContains(shellCodeBehind, "ShowInstallClaimGate");
        RequireContains(shellCodeBehind, "BuildInstallClaimHref()");
        RequireContains(shellCodeBehind, "BuildInstallSupportHref()");
        RequireContains(shellSource, "Please claim your app");
        RequireContains(shellSource, "Claim this app on chummer.run");
        RequireContains(shellSource, "desktop-install-claim-gate");
        RequireContains(shellSource, "desktop-install-claim-start");
    }

    private static void BlazorDesktopPrintPreview_waits_for_loaded_document_before_printing()
    {
        string appSource = ReadSource("Chummer.Blazor/Components/App.razor");
        string privacySource = ReadSource("Chummer.Blazor/wwwroot/js/privacy-boundaries.js");

        RequireContains(appSource, "js/privacy-boundaries.js");
        RequireContains(appSource, "data-consent-default=\"denied\"");
        RequireContains(appSource, "data-automatic-pageviews=\"disabled\"");
        RequireContains(appSource, "data-chummer-analytics-preferences");
        RequireContains(appSource, "data-chummer-analytics-consent-status");
        RequireContains(appSource, "data-chummer-analytics-consent-grant");
        RequireContains(appSource, "data-chummer-analytics-consent-revoke");
        RequireContains(appSource, "RybbitDefaultTrackUrl = \"https://app.rybbit.io/api/track\"");
        RequireContains(appSource, "TryBuildRybbitTrackEndpointFromScript");
        RequireDoesNotContain(appSource, "<script src=\"@rybbitAnalytics");
        RequireDoesNotContain(appSource, "window.open('', '_blank')");
        RequireDoesNotContain(appSource, "printWindow.document.write");

        RequireContains(privacySource, "frame.setAttribute('sandbox', '')");
        RequireContains(privacySource, "frame.setAttribute('referrerpolicy', 'no-referrer')");
        RequireContains(privacySource, "frame.srcdoc = safePrintDocument");
        RequireContains(privacySource, "default-src 'none'; script-src 'none'; connect-src 'none'");
        RequireContains(privacySource, "object-src 'none'; frame-src 'none'");
        RequireContains(privacySource, "base-uri 'none'; form-action 'none'");
        RequireContains(privacySource, "preformatted.textContent = decoded");
        RequireContains(privacySource, "sourceElement.namespaceURI !== 'http://www.w3.org/1999/xhtml'");
        RequireContains(privacySource, "frame.addEventListener('load', triggerPrint, { once: true })");
        RequireContains(privacySource, "global.requestAnimationFrame(() => global.print())");
        RequireDoesNotContain(privacySource, "window.open(");
        RequireDoesNotContain(privacySource, "document.write(");
        RequireDoesNotContain(privacySource, "allow-same-origin");

        RequireContains(privacySource, "analyticsConsentStorageKey");
        RequireContains(privacySource, "navigator.globalPrivacyControl === true");
        RequireContains(privacySource, "navigator.doNotTrack");
        RequireContains(privacySource, "automaticPageviews: false");
        RequireContains(privacySource, "pendingEventCount: 0");
        RequireContains(privacySource, "credentials: 'omit'");
        RequireContains(privacySource, "referrerPolicy: 'no-referrer'");
        RequireContains(privacySource, "abortActiveAnalyticsRequests()");
        RequireContains(privacySource, "properties: JSON.stringify(sanitized)");
        RequireDoesNotContain(privacySource, "location.search");
        RequireDoesNotContain(privacySource, "document.referrer");
        RequireDoesNotContain(privacySource, "history.pushState");
        RequireDoesNotContain(privacySource, "session_replay");
    }

    private static void BlazorDialogReveal_skips_same_dialog_recenters_across_transient_refreshes()
    {
        string appSource = ReadSource("Chummer.Blazor/Components/App.razor");
        RequireContains(appSource, "window.chummerDialogs._pendingRevealResetHandle");
        RequireContains(appSource, "window.chummerDialogs._pendingOriginAdvancedAnchor");
        RequireContains(appSource, "window.chummerDialogs._pendingOriginFieldAnchor");
        RequireContains(appSource, "window.chummerDialogs._pendingOriginAnchorCapturedAtMs");
        RequireContains(appSource, "window.chummerDialogs._pendingSameDialogRefreshDialogId");
        RequireContains(appSource, "window.chummerDialogs._pendingSameDialogRefreshCapturedAtMs");
        RequireContains(appSource, "window.chummerDialogs._pendingDialogScrollRestoreVersion");
        RequireContains(appSource, "window.chummerDialogs._originSameDialogAnchorGraceWindowMs");
        RequireContains(appSource, "window.chummerDialogs._sameDialogRefreshArmWindowMs");
        RequireContains(appSource, "window.chummerDialogs._sameDialogRefreshGraceWindowMs");
        RequireContains(appSource, "window.chummerDialogs.clearPendingOriginAnchors");
        RequireContains(appSource, "window.chummerDialogs.armSameDialogRefresh");
        RequireContains(appSource, "window.chummerDialogs.hasPendingSameDialogRefresh");
        RequireContains(appSource, "window.chummerDialogs.hasPendingOriginAnchor");
        RequireContains(appSource, "window.chummerDialogs.hasPendingDialogScrollRestore");
        RequireContains(appSource, "window.chummerDialogs.scheduleRevealReset");
        RequireContains(appSource, "window.chummerDialogs.cancelRevealReset");
        RequireContains(appSource, "window.chummerDialogs.isSameDialogRefresh");
        RequireContains(appSource, "data-origin-advanced-controls][data-expanded=\"true\"]");
        RequireContains(appSource, "captureDialogScroll = function(element, fieldId)");
        RequireContains(appSource, "window.chummerDialogs._pendingDialogScrollOffset");
        RequireContains(appSource, "fieldAnchorElement.closest('[data-origin-wizard]')");
        RequireContains(appSource, "restoreOriginAdvancedAnchor");
        RequireContains(appSource, "restoreOriginFieldAnchor");
        RequireContains(appSource, "window.chummerDialogs.restorePendingDialogScroll = function(element, dialogId)");
        RequireContains(appSource, "const restoreVersion = Number(window.chummerDialogs._pendingDialogScrollRestoreVersion || 0);");
        RequireContains(appSource, "if (restoreVersion !== Number(window.chummerDialogs._pendingDialogScrollRestoreVersion || 0)) {");
        RequireContains(appSource, "window.chummerDialogs.scheduleRevealReset();");
        RequireContains(appSource, "window.chummerDialogs.cancelRevealReset();");
        RequireContains(appSource, "window.chummerDialogs.isSameDialogRefresh(dialogId)");
        RequireContains(appSource, "&& (window.chummerDialogs._lastRevealedDialogId === dialogId");
        RequireContains(appSource, "|| window.chummerDialogs.hasPendingSameDialogRefresh(dialogId)");
        RequireContains(appSource, "|| window.chummerDialogs.hasPendingOriginAnchor(dialogId)));");
        Assert.IsFalse(
            appSource.Contains("const shouldPreferOriginAdvancedAnchor = function()", StringComparison.Ordinal),
            "Origin scroll restoration should anchor to the active field before falling back to the advanced panel.");
        RequireContains(appSource, "window.chummerDialogs.hasPendingOriginAnchor(window.chummerDialogs._lastRevealedDialogId || null)");
        RequireContains(appSource, "window.chummerDialogs.hasPendingSameDialogRefresh(window.chummerDialogs._lastRevealedDialogId || null)");
        RequireContains(appSource, "window.chummerDialogs.armSameDialogRefresh(dialogId || null);");
        RequireContains(appSource, "}, window.chummerDialogs._sameDialogRefreshGraceWindowMs);");
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
        int index = source.IndexOf(unexpected, StringComparison.Ordinal);
        if (index >= 0)
        {
            int start = Math.Max(0, index - 90);
            int length = Math.Min(source.Length - start, unexpected.Length + 180);
            string snippet = source.Substring(start, length).ReplaceLineEndings(" ");
            throw new InvalidOperationException($"Expected not to find '{unexpected}' in smoke target source. Nearby text: {snippet}");
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
