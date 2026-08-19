using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Nodes;

namespace Chummer.Presentation.Overview;

[TestClass]
public sealed class BuildGhostAlicePresentationTests
{
    private const string SourceDigest = "sha256:1111111111111111111111111111111111111111111111111111111111111111";
    private const string InputDigest = "sha256:2222222222222222222222222222222222222222222222222222222222222222";

    [TestMethod]
    public void Every_shipping_locale_materializes_complete_Rook_identity_without_fallback()
    {
        CollectionAssert.AreEquivalent(
            DesktopLocalizationCatalog.ShippingLanguages.Select(static language => language.Code).ToArray(),
            BuildGhostAlicePresentation.MaterializedLocaleCodes.ToArray());
        foreach (DesktopSupportedLanguage language in DesktopLocalizationCatalog.ShippingLanguages)
        {
            IReadOnlyList<DesktopDialogField> fields = BuildGhostAlicePresentation.CreateInterviewFields(language.Code);

            DesktopDialogField identity = fields.Single(field => field.Id == "autoAliceBuildGhostIdentity");
            StringAssert.Contains(identity.Value, BuildGhostAlicePresentation.PersonaId);
            StringAssert.Contains(identity.Value, BuildGhostAlicePresentation.AvatarId);
            StringAssert.Contains(identity.Value, BuildGhostAlicePresentation.VoiceId);
            Assert.IsFalse(identity.Value.Contains("fallback", StringComparison.OrdinalIgnoreCase));
            Assert.HasCount(4, fields);
        }
    }

    [TestMethod]
    public void Unsupported_locale_fails_closed_instead_of_silently_using_English()
        => Assert.Throws<NotSupportedException>(() => BuildGhostAlicePresentation.CreateInterviewFields("es-es"));

    [TestMethod]
    public void Digest_bound_packet_renders_three_compare_cards_and_preview_only_choice()
    {
        DesktopDialogState dialog = CreateDialog("en-us");
        JsonObject packet = CreatePacket("en-US");
        packet["packetDigest"] = BuildGhostAlicePresentation.ComputePacketDigest(packet);
        dialog = BuildGhostAlicePresentation.BindPacket(dialog, packet.ToJsonString());

        IReadOnlyList<DesktopDialogField> fields = BuildGhostAlicePresentation.AppendPreviewFields(
            dialog.Fields,
            new CharacterOverviewState(new TestWorkspaceId("runner-1"), 7),
            out bool previewable);

        Assert.IsTrue(previewable);
        Assert.HasCount(3, fields.Where(field => field.Id.StartsWith("autoAliceBuildGhostPreviewVariant_", StringComparison.Ordinal)));
        DesktopDialogField selector = fields.Single(field => field.Id == BuildGhostAlicePresentation.SelectedVariantFieldId);
        Assert.HasCount(3, selector.Options!);
        Assert.IsTrue(fields.Single(field => field.Id == "autoAliceBuildGhostPreviewFacts").Value.Contains("anchor:matrix", StringComparison.Ordinal));
        Assert.IsFalse(fields.Single(field => field.Id == "autoAliceBuildGhostPreviewGroup").Value.Contains("member-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Tampered_packet_uses_safe_local_fallback_and_exposes_no_preview_action()
    {
        DesktopDialogState dialog = CreateDialog("de-de");
        JsonObject packet = CreatePacket("de-DE");
        packet["packetDigest"] = BuildGhostAlicePresentation.ComputePacketDigest(packet);
        packet["sourceDigest"] = "sha256:tampered";
        dialog = BuildGhostAlicePresentation.BindPacket(dialog, packet.ToJsonString());

        IReadOnlyList<DesktopDialogField> fields = BuildGhostAlicePresentation.AppendPreviewFields(
            dialog.Fields,
            new CharacterOverviewState(null, 0),
            out bool previewable);

        Assert.IsFalse(previewable);
        StringAssert.Contains(fields.Single(field => field.Id == "autoAliceBuildGhostPreviewStatus").Value, "digest");
        Assert.IsFalse(fields.Any(field => field.Id.StartsWith("autoAliceBuildGhostPreviewVariant_", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Preview_receipt_is_digest_and_revision_bound_and_does_not_claim_a_mutation()
    {
        DesktopDialogState dialog = CreateDialog("fr-fr");
        JsonObject packet = CreatePacket("fr-FR");
        packet["packetDigest"] = BuildGhostAlicePresentation.ComputePacketDigest(packet);
        dialog = BuildGhostAlicePresentation.BindPacket(dialog, packet.ToJsonString());
        IReadOnlyList<DesktopDialogField> fields = BuildGhostAlicePresentation.AppendPreviewFields(
            dialog.Fields,
            new CharacterOverviewState(new TestWorkspaceId("runner-1"), 7),
            out bool previewable);
        Assert.IsTrue(previewable);
        dialog = dialog with { Fields = fields };

        bool accepted = BuildGhostAlicePresentation.TryCreatePreviewReceipt(
            dialog,
            new CharacterOverviewState(new TestWorkspaceId("runner-1"), 7),
            out string receipt,
            out string error);

        Assert.IsTrue(accepted, error);
        StringAssert.Contains(receipt, "sha256:");
        StringAssert.Contains(receipt, $"input={InputDigest}");
        StringAssert.Contains(receipt, $"source={SourceDigest}");
        StringAssert.Contains(receipt, "revision=7");
        StringAssert.Contains(receipt, "No dossier mutation was performed.");
    }

    [TestMethod]
    public void Preview_plan_with_a_mismatched_source_binding_is_visible_but_not_selectable()
    {
        DesktopDialogState dialog = CreateDialog("en-US");
        JsonObject packet = CreatePacket("en-US");
        JsonObject firstVariant = packet["variants"]!.AsArray()[0]!.AsObject();
        firstVariant["applyPreview"]!.AsObject()["expectedSourceDigest"] =
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
        packet["packetDigest"] = BuildGhostAlicePresentation.ComputePacketDigest(packet);
        dialog = BuildGhostAlicePresentation.BindPacket(dialog, packet.ToJsonString());

        IReadOnlyList<DesktopDialogField> fields = BuildGhostAlicePresentation.AppendPreviewFields(
            dialog.Fields,
            new CharacterOverviewState(new TestWorkspaceId("runner-1"), 7),
            out bool previewable);

        Assert.IsTrue(previewable);
        DesktopDialogField selector = fields.Single(field => field.Id == BuildGhostAlicePresentation.SelectedVariantFieldId);
        Assert.HasCount(2, selector.Options!);
        Assert.IsFalse(selector.Options!.Any(option => string.Equals(
            option.Value,
            "runner-1:conservative-repair:v1",
            StringComparison.Ordinal)));
        StringAssert.Contains(
            fields.Single(field => field.Id == "autoAliceBuildGhostPreviewVariant_conservative-repair").Value,
            "build-ghost-preview-binding-mismatch");
    }

    [TestMethod]
    public async Task Current_workspace_packet_loader_uses_catalog_locale_context_and_binds_the_packet()
    {
        DesktopDialogState dialog = CreateDialog("de-de");
        JsonObject packet = CreatePacket("de-DE");
        packet["packetDigest"] = BuildGhostAlicePresentation.ComputePacketDigest(packet);
        string? observedWorkspace = null;
        string? observedLocale = null;
        IReadOnlyList<string>? observedSupportedLocales = null;
        string? observedFallback = null;

        DesktopDialogState bound = await BuildGhostAlicePacketLoader.BindCurrentWorkspacePacketAsync(
            dialog,
            "runner-1",
            "de-de",
            (workspace, locale, supportedLocales, fallback, _) =>
            {
                observedWorkspace = workspace;
                observedLocale = locale;
                observedSupportedLocales = supportedLocales;
                observedFallback = fallback;
                return Task.FromResult<string?>(packet.ToJsonString());
            },
            CancellationToken.None);

        Assert.AreEqual("runner-1", observedWorkspace);
        Assert.AreEqual("de-de", observedLocale);
        CollectionAssert.AreEquivalent(
            new[] { "en-US", "de-DE", "fr-FR", "ja-JP", "pt-BR", "zh-CN" },
            observedSupportedLocales!.ToArray());
        StringAssert.Contains(observedFallback, "lokale Erklärung");
        Assert.IsFalse(string.IsNullOrWhiteSpace(bound.Fields.Single(field => field.Id == BuildGhostAlicePresentation.PacketFieldId).Value));
    }

    [TestMethod]
    public async Task Current_workspace_packet_loader_fails_closed_when_analysis_is_unavailable()
    {
        DesktopDialogState dialog = CreateDialog("en-us");

        DesktopDialogState unchanged = await BuildGhostAlicePacketLoader.BindCurrentWorkspacePacketAsync(
            dialog,
            "runner-1",
            "en-us",
            static (_, _, _, _, _) => throw new InvalidOperationException("engine unavailable"),
            CancellationToken.None);

        Assert.IsTrue(string.IsNullOrWhiteSpace(unchanged.Fields.Single(field => field.Id == BuildGhostAlicePresentation.PacketFieldId).Value));
        IReadOnlyList<DesktopDialogField> fields = BuildGhostAlicePresentation.AppendPreviewFields(
            unchanged.Fields,
            new CharacterOverviewState(new TestWorkspaceId("runner-1"), 7),
            out bool previewable);
        Assert.IsFalse(previewable);
        StringAssert.Contains(fields.Single(field => field.Id == "autoAliceBuildGhostPreviewStatus").Value, "Waiting");
    }

    private static DesktopDialogState CreateDialog(string locale)
        => new(
            "dialog.auto_alice",
            "Auto ALICE",
            null,
            BuildGhostAlicePresentation.CreateInterviewFields(locale),
            []);

    private static JsonObject CreatePacket(string locale)
    {
        JsonArray variants =
        [
            Variant("runner-1:conservative-repair:v1", "conservative-repair"),
            Variant("runner-1:role-focused-specialization:v1", "role-focused-specialization"),
            Variant("runner-1:balanced-hybrid:v1", "balanced-hybrid")
        ];
        return new JsonObject
        {
            ["schema"] = BuildGhostAlicePresentation.AnalysisSchema,
            ["personaId"] = BuildGhostAlicePresentation.PersonaId,
            ["avatarId"] = BuildGhostAlicePresentation.AvatarId,
            ["voiceId"] = BuildGhostAlicePresentation.VoiceId,
            ["locale"] = locale,
            ["workspaceId"] = "runner-1",
            ["workspaceRevision"] = 7,
            ["sourceDigest"] = SourceDigest,
            ["inputDigest"] = InputDigest,
            ["packetDigest"] = string.Empty,
            ["strengths"] = new JsonArray(new JsonObject
            {
                ["label"] = "Matrix pool",
                ["value"] = "12",
                ["sourceAnchorIds"] = new JsonArray("anchor:matrix")
            }),
            ["blockers"] = new JsonArray(),
            ["warnings"] = new JsonArray(),
            ["tips"] = new JsonArray(new JsonObject
            {
                ["explanation"] = "Raise the grounded matrix breakpoint.",
                ["expectedBenefit"] = "+2 dice",
                ["opportunityCost"] = "2 skill points",
                ["risk"] = "lower breadth",
                ["sourceAnchorIds"] = new JsonArray("anchor:matrix")
            }),
            ["ruleExplanations"] = new JsonArray(new JsonObject
            {
                ["question"] = "How is this pool calculated?",
                ["status"] = "resolved",
                ["explanation"] = "Logic plus Hacking plus modifiers.",
                ["sourceAnchorIds"] = new JsonArray("anchor:matrix")
            }),
            ["variants"] = variants,
            ["allowedSuggestedActions"] = new JsonArray(
                AllowedAction("runner-1:conservative-repair:v1"),
                AllowedAction("runner-1:role-focused-specialization:v1"),
                AllowedAction("runner-1:balanced-hybrid:v1")),
            ["groupCapabilityPosture"] = new JsonObject
            {
                ["visibilityPosture"] = "consented-visible-scope",
                ["visibleMembers"] = new JsonArray(new JsonObject { ["memberRef"] = "member-secret" }),
                ["conclusions"] = new JsonArray(new JsonObject { ["wording"] = "No visible member covers First Aid." })
            }
        };
    }

    private static JsonObject Variant(string id, string shape)
        => new()
        {
            ["variantId"] = id,
            ["shape"] = shape,
            ["inputDigest"] = InputDigest,
            ["shortTermBenefit"] = "Immediate grounded improvement",
            ["longTermCeiling"] = "Clear advancement path",
            ["costsAndLostAlternatives"] = new JsonArray("one alternative delayed"),
            ["dependencies"] = new JsonArray("source:core"),
            ["gmPolicyConflicts"] = new JsonArray(),
            ["validation"] = new JsonObject
            {
                ["status"] = "available",
                ["blockers"] = new JsonArray(),
                ["warnings"] = new JsonArray()
            },
            ["applyPreview"] = new JsonObject
            {
                ["actionId"] = $"preview:{id}",
                ["actionType"] = "chummer.preview_build_variant",
                ["variantId"] = id,
                ["previewOnly"] = true,
                ["requiresExplicitReview"] = true,
                ["expectedWorkspaceRevision"] = 7,
                ["expectedSourceDigest"] = SourceDigest,
                ["expectedInputDigest"] = InputDigest
            }
        };

    private static JsonObject AllowedAction(string variantId)
        => new()
        {
            ["actionId"] = $"preview:{variantId}",
            ["actionType"] = "chummer.preview_build_variant",
            ["variantId"] = variantId,
            ["requiresExplicitReview"] = true,
            ["workspaceRevision"] = 7,
            ["sourceDigest"] = SourceDigest
        };
}
