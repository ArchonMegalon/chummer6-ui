#nullable enable annotations

using System;
using Bunit;
using Chummer.Blazor.Components.Pages;
using Chummer.Blazor.Components.Shared;
using Chummer.Campaign.Contracts;
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
            Variants: [],
            ProgressionTimelines: [],
            NextSafeAction: "Rebind runtime before export.",
            RuntimeCompatibilitySummary: "One provider binding still needs review.",
            CampaignFitSummary: "Fits sparse-ops crews.",
            SupportClosureSummary: "Support can cite the same runtime fingerprint.",
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
        Assert.IsNotNull(cut.Find("[data-build-lab-decision-rail]"));
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
        StringAssert.Contains(cut.Markup, "Rebind runtime before dossier handoff.");
        StringAssert.Contains(cut.Markup, "chummer-explain-chip");
        StringAssert.Contains(cut.Markup, "chummer-card-artifact");
        Assert.IsNotNull(cut.Find("[data-build-lab-handoff-rail]"));
    }

    [TestMethod]
    public void RulesNavigatorPanel_renders_grounded_answer_and_reuse_hints()
    {
        RulesNavigatorAnswerProjection projection = new(
            EntryId: "rules-1",
            Question: "Why did this rule posture change?",
            ShortAnswer: "Because the approved compatibility fingerprint changed.",
            BeforeSummary: "Before approval, the answer path was caveated.",
            AfterSummary: "After approval, the rule environment is grounded everywhere.",
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
            ]);

        using var context = new BunitContext();
        IRenderedComponent<RulesNavigatorPanel> cut = context.Render<RulesNavigatorPanel>(parameters => parameters
            .Add(component => component.Projection, projection));

        StringAssert.Contains(cut.Markup, "Rules Navigator");
        StringAssert.Contains(cut.Markup, "Why did this rule posture change?");
        StringAssert.Contains(cut.Markup, "Before approval");
        StringAssert.Contains(cut.Markup, "Support can reuse this answer.");
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
            StringAssert.Contains(cut.Markup, "creator packet");
            StringAssert.Contains(cut.Markup, "Campaign-safe decision rail");
        });
    }
}
