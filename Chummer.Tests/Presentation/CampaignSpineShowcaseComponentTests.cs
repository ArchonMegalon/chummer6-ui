#nullable enable annotations

using System;
using Bunit;
using Chummer.Blazor.Components.Pages;
using Chummer.Blazor.Components.Shared;
using Chummer.Campaign.Contracts;
using Chummer.Contracts.Rulesets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BunitContext = Bunit.BunitContext;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CampaignSpineShowcaseComponentTests
{
    [TestMethod]
    public void BuildLabPanel_renders_decision_rail_and_watchouts()
    {
        BuildLabConceptIntakeProjection projection = new(
            WorkspaceId: "showcase.build-lab",
            WorkflowId: "workflow.build-lab",
            Title: "Build Lab Intake",
            Summary: "Grounded intake before campaign handoff.",
            RulesetId: "sr5",
            BuildMethod: "Priority",
            IntakeFields:
            [
                new BuildLabIntakeField("concept", "Concept", BuildLabFieldKinds.Text, "Street operator")
            ],
            RoleBadges:
            [
                new BuildLabBadge("face", "Face", BuildLabBadgeKinds.Role, true)
            ],
            ConstraintBadges:
            [
                new BuildLabBadge("ops", "Ops-first", BuildLabBadgeKinds.Constraint, true)
            ],
            ProvenanceBadges:
            [
                new BuildLabBadge("runtime", "Runtime-backed", BuildLabBadgeKinds.Provenance, true)
            ],
            Variants:
            [
                new BuildLabVariantProjection(
                    VariantId: "variant.social",
                    Label: "Social Operator",
                    Summary: "Fastest ops-first lane.",
                    TableFit: "Ops-first",
                    RoleBadges: [],
                    Metrics:
                    [
                        new BuildLabVariantMetric("coverage", "Coverage", "Improved")
                    ],
                    Warnings: [],
                    OverlapBadges:
                    [
                        new BuildLabBadge("face-overlap", "Light face overlap", BuildLabBadgeKinds.Overlap)
                    ],
                    Actions: [],
                    ExplainEntryId: "buildlab.variant.social")
            ],
            ProgressionTimelines:
            [
                new BuildLabProgressionTimeline(
                    TimelineId: "timeline.social",
                    Title: "Social Operator Ladder",
                    Summary: "25 / 50 / 100 Karma checkpoints.",
                    VariantId: "variant.social",
                    Steps:
                    [
                        new BuildLabProgressionStep(
                            "social-25",
                            25,
                            "Opener",
                            "Table-ready lead.",
                            Outcomes: [],
                            MilestoneBadges:
                            [
                                new BuildLabBadge("25", "25 Karma", BuildLabBadgeKinds.Milestone, true)
                            ],
                            RiskBadges:
                            [
                                new BuildLabBadge("astral-gap", "Astral gap", BuildLabBadgeKinds.Risk)
                            ],
                            ExplainEntryId: "buildlab.timeline.social-25"),
                        new BuildLabProgressionStep(
                            "social-100",
                            100,
                            "Anchor",
                            "Campaign-ready anchor.",
                            Outcomes:
                            [
                                new BuildLabVariantMetric("coverage", "Coverage", "Broad")
                            ],
                            MilestoneBadges: [],
                            RiskBadges: [],
                            ExplainEntryId: "buildlab.timeline.social-100")
                    ],
                    SourceDocumentId: "source.timeline")
            ],
            NextSafeAction: "Rebind runtime before export.",
            RuntimeCompatibilitySummary: "One provider binding still needs review.",
            CampaignFitSummary: "Fits sparse-ops crews.",
            SupportClosureSummary: "Support can cite the same runtime fingerprint.",
            TeamCoverage: new BuildLabTeamCoverageProjection(
                Summary: "2 of 3 required crew roles are covered before handoff; one deliberate face overlap stays visible while astral support remains missing.",
                CoverageSummary: "Coverage score stays stable with Face and Legwork already covered before the first campaign handoff.",
                RolePressureSummary: "Role pressure stays light because the duplicate face lane is intentional, but astral support still needs a partner runner.",
                MissingRoleTags: ["astral"],
                CoveredRoleTags: ["face", "legwork"],
                DuplicateRoleTags: ["face"],
                ExplainEntryId: "buildlab.teamcoverage.ops-first"),
            Watchouts:
            [
                "Do not export until the runtime rebind receipt exists."
            ]);

        using var context = new BunitContext();
        IRenderedComponent<BuildLabPanel> cut = context.Render<BuildLabPanel>(parameters => parameters
            .Add(component => component.Projection, projection));

        StringAssert.Contains(cut.Markup, "Campaign-safe decision rail");
        StringAssert.Contains(cut.Markup, "Rebind runtime before export.");
        StringAssert.Contains(cut.Markup, "Fits sparse-ops crews.");
        StringAssert.Contains(cut.Markup, "Do not export until the runtime rebind receipt exists.");
        StringAssert.Contains(cut.Markup, "Planner + team coverage");
        StringAssert.Contains(cut.Markup, "Covered roles: Face | Legwork");
        StringAssert.Contains(cut.Markup, "Missing roles: Astral");
        StringAssert.Contains(cut.Markup, "Duplicate roles: Face");
        StringAssert.Contains(cut.Markup, "Light face overlap");
        StringAssert.Contains(cut.Markup, "strongest coverage checkpoint at 100 Karma");
        StringAssert.Contains(cut.Markup, "buildlab.timeline.social-25");
        StringAssert.Contains(cut.Markup, "25 Karma");
        StringAssert.Contains(cut.Markup, "Astral gap");
        Assert.IsNotNull(cut.Find("[data-build-lab-decision-rail]"));
        Assert.IsNotNull(cut.Find("[data-build-lab-optimizer-rail]"));
        Assert.IsNotNull(cut.Find("[data-build-lab-timeline-step-badges='social-25']"));
    }

    [TestMethod]
    public void BuildLabHandoffPanel_renders_dossier_and_campaign_outputs()
    {
        BuildLabHandoffProjection projection = new(
            HandoffId: "handoff-1",
            DossierId: "dossier-1",
            CampaignId: "campaign-1",
            Title: "Ops handoff",
            Summary: "Chosen build lane lands in campaign truth.",
            VariantLabel: "Ops-first",
            ProgressionLabel: "25 / 50 / 100 Karma path",
            ExplainEntryId: "buildlab.handoff.1",
            TradeoffLines:
            [
                "Role overlap stays explicit."
            ],
            ProgressionOutcomes:
            [
                "Output remains attached to the living dossier."
            ],
            Outputs:
            [
                new PublicationSafeProjection("projection-1", "dossier_card", "Living dossier", "Stable runner identity.", "artifact-1")
            ],
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            NextSafeAction: "Rebind runtime before dossier handoff.",
            RuntimeCompatibilitySummary: "Runtime drift still needs one safe rebind.",
            CampaignReturnSummary: "Campaign return lands on the same dossier identity.",
            SupportClosureSummary: "Support closure can cite the same runtime fingerprint.",
            PlannerCoverageSummary: "4 of 4 build follow-through checkpoints are already grounded.",
            PlannerCoverageLines:
            [
                "Campaign continuity: Neon Nights is already attached as the governed return lane for this handoff.",
                "Outputs: 1 dossier or campaign-safe output is already attached to the handoff.",
                "Restore posture: no restore conflicts are currently blocking replay-safe handoff follow-through.",
                "Claimed install: 1 linked device is already attached for install-aware follow-through."
            ],
            Watchouts:
            [
                "Do not publish until the runtime rebind is recorded."
            ]);

        using var context = new BunitContext();
        IRenderedComponent<BuildLabHandoffPanel> cut = context.Render<BuildLabHandoffPanel>(parameters => parameters
            .Add(component => component.Projection, projection));

        StringAssert.Contains(cut.Markup, "Ops handoff");
        StringAssert.Contains(cut.Markup, "Living dossier");
        StringAssert.Contains(cut.Markup, "artifact-1");
        StringAssert.Contains(cut.Markup, "25 / 50 / 100 Karma path");
        StringAssert.Contains(cut.Markup, "Campaign-safe handoff");
        StringAssert.Contains(cut.Markup, "Planner coverage");
        StringAssert.Contains(cut.Markup, "4 of 4 build follow-through checkpoints are already grounded.");
        StringAssert.Contains(cut.Markup, "Campaign continuity: Neon Nights is already attached as the governed return lane for this handoff.");
        StringAssert.Contains(cut.Markup, "Rebind runtime before dossier handoff.");
        StringAssert.Contains(cut.Markup, "chummer-explain-chip");
        StringAssert.Contains(cut.Markup, "chummer-card-artifact");
        Assert.IsNotNull(cut.Find("[data-build-lab-handoff-rail]"));
        Assert.IsNotNull(cut.Find("[data-build-lab-handoff-planner-coverage]"));
    }

    [TestMethod]
    public void RulesNavigatorPanel_renders_grounded_answer_and_reuse_hints()
    {
        RulesNavigatorAnswerProjection projection = new(
            EntryId: "rules-1",
            Question: "Why did this rule posture change?",
            ShortAnswer: "Because the campaign-approved compatibility fingerprint changed.",
            BeforeSummary: "Before campaign approval, the answer path was caveated.",
            AfterSummary: "After campaign approval, the rule environment is grounded everywhere.",
            ExplainEntryId: "rules.navigator.1",
            ProvenanceLabel: "campaign scope · sr6.preview.v1",
            EvidenceLines:
            [
                "sr6.preview.v1 is active.",
                "GM readiness and support reuse the same fingerprint."
            ],
            SupportReuseHints:
            [
                "Support can reuse this answer."
            ],
            Diffs:
            [
                new RulesetEnvironmentDiffProjection(
                    DiffId: "rules-1:return",
                    Label: "Campaign return",
                    BeforeSummary: "Before campaign approval, the answer path was caveated.",
                    AfterSummary: "After campaign approval, the rule environment is grounded everywhere.",
                    ReasonSummary: "Campaign continuity reuses the campaign-approved compatibility fingerprint.",
                    ExplainEntryId: "rules.navigator.1:return")
            ],
            Studio: new RuleEnvironmentStudioProjection(
                CurrentStage: RuleEnvironmentLifecycleStages.CampaignApproved,
                CurrentStageLabel: "Campaign-approved",
                PromotionTargetStage: RuleEnvironmentLifecycleStages.Published,
                PromotionTargetLabel: "Published",
                PromotionSummary: "Promote the current campaign-approved fingerprint only when broader reuse is intentional.",
                RollbackSummary: "Rollback can re-pin sr6.preview.v1 on the current campaign while the next promotion is reviewed.",
                LineageSummary: "The current campaign fingerprint remains the lineage anchor until a published successor replaces it.",
                Stages:
                [
                    new RuleEnvironmentLifecycleStepProjection(RuleEnvironmentLifecycleStages.Sandbox, "Sandbox", RuleEnvironmentLifecycleStepStatuses.Completed, "Preview work stays bounded while validation settles."),
                    new RuleEnvironmentLifecycleStepProjection(RuleEnvironmentLifecycleStages.CampaignApproved, "Campaign-approved", RuleEnvironmentLifecycleStepStatuses.Current, "The current campaign answer is already governed."),
                    new RuleEnvironmentLifecycleStepProjection(RuleEnvironmentLifecycleStages.Published, "Published", RuleEnvironmentLifecycleStepStatuses.Next, "Broader reuse waits for explicit promotion.")
                ]));

        using var context = new BunitContext();
        IRenderedComponent<RulesNavigatorPanel> cut = context.Render<RulesNavigatorPanel>(parameters => parameters
            .Add(component => component.Projection, projection));

        StringAssert.Contains(cut.Markup, "Rules Navigator");
        StringAssert.Contains(cut.Markup, "Why did this rule posture change?");
        StringAssert.Contains(cut.Markup, "Before campaign approval");
        StringAssert.Contains(cut.Markup, "Campaign return");
        StringAssert.Contains(cut.Markup, "Campaign continuity reuses the campaign-approved compatibility fingerprint.");
        StringAssert.Contains(cut.Markup, "Support can reuse this answer.");
        StringAssert.Contains(cut.Markup, "Rule-environment studio");
        StringAssert.Contains(cut.Markup, "Campaign-approved -&gt; Published");
        StringAssert.Contains(cut.Markup, "Rollback can re-pin sr6.preview.v1");
        StringAssert.Contains(cut.Markup, "lineage anchor");
        StringAssert.Contains(cut.Markup, "chummer-explain-chip");
    }

    [TestMethod]
    public void CreatorPublicationPanel_renders_trusted_publication_posture()
    {
        CreatorPublicationProjection publication = new(
            PublicationId: "publication-1",
            Title: "Creator packet",
            Kind: "campaign_packet",
            Summary: "Creator outputs share one governed shelf.",
            CampaignId: "campaign-1",
            DossierId: "dossier-1",
            ArtifactId: "artifact-publication",
            ProvenanceSummary: "Approved rule fingerprint + recap shelf",
            DiscoverySummary: "Preview-ready discovery posture",
            Visibility: "group",
            PublicationStatus: "preview_ready",
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        using var context = new BunitContext();
        IRenderedComponent<CreatorPublicationPanel> cut = context.Render<CreatorPublicationPanel>(parameters => parameters
            .Add(component => component.Publication, publication));

        StringAssert.Contains(cut.Markup, "Creator packet");
        StringAssert.Contains(cut.Markup, "artifact-publication");
        StringAssert.Contains(cut.Markup, "Preview-ready discovery posture");
        StringAssert.Contains(cut.Markup, "preview_ready");
    }

    [TestMethod]
    public void Home_renders_build_lab_rules_and_creator_showcase_panels()
    {
        using var context = new BunitContext();
        IRenderedComponent<Home> cut = context.Render<Home>();

        cut.WaitForAssertion(() =>
        {
            Assert.IsNotNull(cut.Find("[data-build-lab-handoff-showcase='handoff.showcase.social-operator']"));
            Assert.IsNotNull(cut.Find("[data-rules-navigator-showcase='rules.navigator.showcase']"));
            Assert.IsNotNull(cut.Find("[data-creator-publication-showcase='publication.showcase.creator-packet']"));
            Assert.IsNotNull(cut.Find("[data-build-lab-decision-rail]"));
            Assert.IsNotNull(cut.Find("[data-build-lab-handoff-rail]"));
            StringAssert.Contains(cut.Markup, "Social Operator build path");
            StringAssert.Contains(cut.Markup, "Rules Navigator");
            StringAssert.Contains(cut.Markup, "campaign packet");
            StringAssert.Contains(cut.Markup, "Campaign-safe decision rail");
        });
    }
}
