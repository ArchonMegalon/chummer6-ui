using System;
using Chummer.Avalonia;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class AvaloniaCoachSidecarProjectorTests
{
    [TestMethod]
    public void Project_formats_budget_provider_and_audit_state_for_active_runtime()
    {
        AiGatewayStatusProjection status = new(
            Status: "scaffolded",
            Routes: [AiRouteTypes.Chat, AiRouteTypes.Coach, AiRouteTypes.Build, AiRouteTypes.Docs, AiRouteTypes.Recap],
            Providers:
            [
                new AiProviderDescriptor(
                    ProviderId: AiProviderIds.AiMagicx,
                    DisplayName: "AI Magicx",
                    SupportsToolCalling: true,
                    SupportsStreaming: true,
                    SupportsAttachments: false,
                    SupportsConversationMemory: true,
                    AllowedRouteTypes: [AiRouteTypes.Coach, AiRouteTypes.Build],
                    AdapterKind: AiProviderAdapterKinds.RemoteHttp,
                    LiveExecutionEnabled: true,
                    AdapterRegistered: true,
                    IsConfigured: true,
                    PrimaryCredentialCount: 1,
                    TransportBaseUrlConfigured: true,
                    TransportModelConfigured: true,
                    TransportMetadataConfigured: true)
            ],
            Tools: [],
            RoutePolicies: [],
            RouteBudgets:
            [
                new AiRouteBudgetPolicyDescriptor(
                    RouteType: AiRouteTypes.Coach,
                    BudgetUnit: AiBudgetUnits.ChummerAiUnits,
                    MonthlyAllowance: 400,
                    BurstLimitPerMinute: 6,
                    Notes: "Coach route policy")
            ],
            RetrievalCorpora: [],
            Budget: new AiBudgetSnapshot(AiBudgetUnits.ChummerAiUnits, 400, 18, 6),
            PromptPolicy: "decker-contact evidence-first",
            RouteBudgetStatuses:
            [
                new AiRouteBudgetStatusProjection(
                    RouteType: AiRouteTypes.Coach,
                    BudgetUnit: AiBudgetUnits.ChummerAiUnits,
                    MonthlyAllowance: 400,
                    MonthlyConsumed: 18,
                    MonthlyRemaining: 382,
                    BurstLimitPerMinute: 6,
                    CurrentBurstConsumed: 1,
                    BurstRemaining: 5,
                    Notes: "Coach route budget")
            ]);

        AiProviderHealthProjection[] providers =
        [
            new(
                ProviderId: AiProviderIds.AiMagicx,
                DisplayName: "AI Magicx",
                AdapterKind: AiProviderAdapterKinds.RemoteHttp,
                AdapterRegistered: true,
                LiveExecutionEnabled: true,
                AllowedRouteTypes: [AiRouteTypes.Coach, AiRouteTypes.Build],
                CircuitState: AiProviderCircuitStates.Closed,
                LastRouteType: AiRouteTypes.Coach,
                LastCredentialTier: AiProviderCredentialTiers.Primary,
                LastCredentialSlotIndex: 0,
                IsConfigured: true,
                PrimaryCredentialCount: 1,
                FallbackCredentialCount: 0,
                TransportBaseUrlConfigured: true,
                TransportModelConfigured: true,
                TransportMetadataConfigured: true,
                LastSuccessAtUtc: new DateTimeOffset(2026, 03, 07, 16, 05, 00, TimeSpan.Zero))
        ];

        AiConversationAuditSummary[] audits =
        [
            new(
                ConversationId: "conv.avalonia-coach-1",
                RouteType: AiRouteTypes.Coach,
                MessageCount: 2,
                LastUpdatedAtUtc: new DateTimeOffset(2026, 03, 07, 16, 10, 00, TimeSpan.Zero),
                RuntimeFingerprint: "sha256:runtime-1",
                LastAssistantAnswer: "Pin the runtime before previewing spend changes.",
                LastProviderId: AiProviderIds.AiMagicx,
                Cache: new AiCacheMetadata(
                    Status: AiCacheStatuses.Hit,
                    CacheKey: "cache::avalonia::coach",
                    CachedAtUtc: new DateTimeOffset(2026, 03, 07, 16, 09, 00, TimeSpan.Zero),
                    NormalizedPrompt: "what next",
                    RuntimeFingerprint: "sha256:runtime-1"),
                RouteDecision: new AiProviderRouteDecision(
                    RouteType: AiRouteTypes.Coach,
                    ProviderId: AiProviderIds.AiMagicx,
                    Reason: "Avalonia coach route stayed grounded.",
                    BudgetUnit: AiBudgetUnits.ChummerAiUnits,
                    ToolingEnabled: true,
                    CredentialTier: AiProviderCredentialTiers.Primary,
                    CredentialSlotIndex: 0),
                GroundingCoverage: new AiGroundingCoverage(
                    ScorePercent: 100,
                    Summary: "runtime and retrieved evidence present.",
                    PresentSignals: ["runtime", "retrieved"],
                    MissingSignals: [],
                    RetrievedCorpusIds: [AiRetrievalCorpusIds.Runtime, AiRetrievalCorpusIds.Community]),
                FlavorLine: "Keep the trace clean and the runtime pinned.",
                Budget: new AiBudgetSnapshot(AiBudgetUnits.ChummerAiUnits, 400, 18, 6, CurrentBurstConsumed: 1),
                StructuredAnswer: new AiStructuredAnswer(
                    Summary: "Preview spend against the active runtime before any mutation path.",
                    Recommendations:
                    [
                        new AiRecommendation(
                            RecommendationId: "rec.avalonia.preview",
                            Title: "Preview spend",
                            Reason: "The grounded lane is already available from the desktop shell.",
                            ExpectedEffect: "Shows non-mutating advancement deltas.")
                    ],
                    Evidence:
                    [
                        new AiEvidenceEntry(
                            Title: "Pinned runtime",
                            Summary: "The active workspace already carries a runtime fingerprint.")
                    ],
                    Risks:
                    [
                        new AiRiskEntry(
                            Severity: "info",
                            Title: "Preview first",
                            Summary: "Skipping preview can hide follow-up validation changes.")
                    ],
                    Confidence: "high",
                    RuntimeFingerprint: "sha256:runtime-1",
                    Sources:
                    [
                        new AiSourceReference(
                            Kind: "runtime",
                            Title: "Active runtime",
                            ReferenceId: "sha256:runtime-1",
                            Source: "avalonia")
                    ],
                    ActionDrafts:
                    [
                        new AiActionDraft(
                            ActionId: AiSuggestedActionIds.PreviewKarmaSpend,
                            Title: "Preview Karma Spend",
                            Description: "Open the guarded preview lane.",
                            RuntimeFingerprint: "sha256:runtime-1")
                    ]))
        ];

        CoachSidecarPaneState projection = MainWindowCoachSidecarProjector.Project(
            status,
            providers,
            audits,
            workspaceId: "ws-1",
            runtimeFingerprint: "sha256:runtime-1",
            launchUri: "/coach/?routeType=coach&runtimeFingerprint=sha256%3Aruntime-1",
            launchStatusMessage: "Scoped Coach launch link copied to the clipboard.",
            errorMessage: null);

        Assert.AreEqual("scaffolded", projection.Status);
        Assert.AreEqual("decker-contact evidence-first", projection.PromptPolicy);
        Assert.AreEqual("382 left / 5 burst", projection.BudgetSummary);
        Assert.AreEqual("ws-1", projection.WorkspaceId);
        Assert.AreEqual("sha256:runtime-1", projection.RuntimeFingerprint);
        Assert.AreEqual("/coach/?routeType=coach&runtimeFingerprint=sha256%3Aruntime-1", projection.LaunchUri);
        Assert.AreEqual("Scoped Coach launch link copied to the clipboard.", projection.LaunchStatusMessage);
        Assert.HasCount(1, projection.Providers);
        Assert.HasCount(1, projection.Audits);
        Assert.AreEqual("AI Magicx", projection.Providers[0].DisplayName);
        Assert.AreEqual("ready · base yes · model yes", projection.Providers[0].TransportSummary);
        Assert.AreEqual("primary 1 / fallback 0", projection.Providers[0].CredentialSummary);
        Assert.AreEqual("route coach · binding primary / slot 0", projection.Providers[0].BindingSummary);
        Assert.AreEqual("cache hit", projection.Audits[0].CacheStatus);
        Assert.AreEqual("/coach/?routeType=coach&conversationId=conv.avalonia-coach-1&runtimeFingerprint=sha256%3Aruntime-1&workspaceId=ws-1", projection.Audits[0].LaunchUri);
        Assert.AreEqual("Pin the runtime before previewing spend changes.", projection.Audits[0].Summary);
        Assert.AreEqual("Keep the trace clean and the runtime pinned.", projection.Audits[0].FlavorLine);
        Assert.AreEqual("18 / 400 chummer-ai-units · burst 1 / 6", projection.Audits[0].BudgetSummary);
        Assert.AreEqual("Preview spend against the active runtime before any mutation path.", projection.Audits[0].StructuredSummary);
        Assert.AreEqual("1 · Preview spend", projection.Audits[0].RecommendationSummary);
    }

    [TestMethod]
    public void Project_returns_safe_defaults_when_gateway_state_is_unavailable()
    {
        CoachSidecarPaneState projection = MainWindowCoachSidecarProjector.Project(
            status: null,
            providerHealth: [],
            audits: [],
            workspaceId: null,
            runtimeFingerprint: null,
            launchUri: null,
            launchStatusMessage: null,
            errorMessage: "offline");

        Assert.AreEqual("unloaded", projection.Status);
        Assert.AreEqual("Policy not loaded yet", projection.PromptPolicy);
        Assert.AreEqual("Budget not loaded yet", projection.BudgetSummary);
        Assert.AreEqual("No workspace attached", projection.WorkspaceId);
        Assert.AreEqual("No runtime fingerprint yet", projection.RuntimeFingerprint);
        Assert.AreEqual(string.Empty, projection.LaunchUri);
        Assert.IsNull(projection.LaunchStatusMessage);
        Assert.AreEqual("offline", projection.ErrorMessage);
        Assert.IsEmpty(projection.Providers);
        Assert.IsEmpty(projection.Audits);
    }
}
