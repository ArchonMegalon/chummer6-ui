using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Chummer.Presentation.Overview;

internal static class BuildGhostAlicePresentation
{
    internal const string AnalysisSchema = "chummer.build_ghost_analysis.v1";
    internal const string PersonaId = "build-ghost-rook-v1";
    internal const string AvatarId = "build-ghost-rook-avatar-v1";
    internal const string VoiceId = "build-ghost-rook-voice-v1";
    internal const string PacketFieldId = "autoAliceBuildGhostAnalysisPacket";
    internal const string LocaleFieldId = "autoAliceBuildGhostLocale";
    internal const string SelectedVariantFieldId = "autoAliceBuildGhostSelectedVariant";
    internal const string PacketDigestFieldId = "autoAliceBuildGhostPacketDigest";
    private const string PreviewBuildVariantActionType = "chummer.preview_build_variant";

    private const string PortraitAsset = "assets/build-ghosts/rook-female-ork-decker-v1.png";

    private static readonly IReadOnlyDictionary<string, Copy> Copies =
        new Dictionary<string, Copy>(StringComparer.Ordinal)
        {
            ["en-us"] = new(
                "Rook · Build Ghost",
                "Adult female ork decker. Rook explains only the deterministic facts Chummer supplied.",
                "Current build",
                "Grounded advice",
                "Rules explanation",
                "Compare three builds",
                "Conservative repair",
                "Role-focused specialization",
                "Balanced hybrid",
                "Short-term benefit",
                "Long-term ceiling",
                "Costs and lost alternatives",
                "Dependencies",
                "Risk and review",
                "Sources",
                "Group capability gaps",
                "Only consented, visible capability bands are summarized; hidden teammate details are never shown.",
                "Preview in Chummer",
                "Rebuild grounded preview",
                "Waiting for Chummer's deterministic analysis packet. Rook will not invent build facts while it is unavailable.",
                "The analysis packet failed schema, identity, locale, revision, or digest validation. Rook is using the safe local fallback.",
                "Available for preview only",
                "Unavailable for preview",
                "Packet binding"),
            ["de-de"] = new(
                "Rook · Build Ghost",
                "Erwachsene Ork-Deckerin. Rook erklärt nur die deterministischen Fakten, die Chummer geliefert hat.",
                "Aktueller Build",
                "Belegte Hinweise",
                "Regelerklärung",
                "Drei Builds vergleichen",
                "Konservative Reparatur",
                "Rollenspezialisierung",
                "Ausgewogener Hybrid",
                "Kurzfristiger Vorteil",
                "Langfristiges Potenzial",
                "Kosten und verlorene Alternativen",
                "Voraussetzungen",
                "Risiko und Prüfung",
                "Quellen",
                "Gruppenfähigkeitslücken",
                "Nur freigegebene, sichtbare Fähigkeitsbereiche werden zusammengefasst; verborgene Teamdetails bleiben unsichtbar.",
                "In Chummer als Vorschau öffnen",
                "Belegte Vorschau neu erstellen",
                "Chummers deterministisches Analysepaket steht noch aus. Rook erfindet solange keine Build-Fakten.",
                "Schema, Identität, Sprache, Revision oder Digest des Analysepakets sind ungültig. Rook nutzt den sicheren lokalen Ersatz.",
                "Nur als Vorschau verfügbar",
                "Keine Vorschau verfügbar",
                "Paketbindung"),
            ["fr-fr"] = new(
                "Rook · Build Ghost",
                "Deckeuse ork adulte. Rook explique uniquement les faits déterministes fournis par Chummer.",
                "Build actuel",
                "Conseils sourcés",
                "Explication de règle",
                "Comparer trois builds",
                "Réparation prudente",
                "Spécialisation de rôle",
                "Hybride équilibré",
                "Bénéfice à court terme",
                "Potentiel à long terme",
                "Coûts et alternatives perdues",
                "Dépendances",
                "Risque et vérification",
                "Sources",
                "Lacunes du groupe",
                "Seules les capacités visibles et consenties sont résumées ; les détails cachés des équipiers ne sont jamais affichés.",
                "Prévisualiser dans Chummer",
                "Reconstruire l'aperçu sourcé",
                "Le paquet d'analyse déterministe de Chummer est attendu. Rook n'inventera aucun fait de build.",
                "Le schéma, l'identité, la langue, la révision ou l'empreinte du paquet est invalide. Rook utilise le repli local sûr.",
                "Disponible uniquement en aperçu",
                "Aperçu indisponible",
                "Liaison du paquet"),
            ["ja-jp"] = new(
                "ルーク · Build Ghost",
                "成人女性オークのデッカー。ルークは Chummer が渡した決定論的な事実だけを説明します。",
                "現在のビルド",
                "根拠付きアドバイス",
                "ルール説明",
                "3つのビルドを比較",
                "保守的な修復",
                "役割特化",
                "バランス型ハイブリッド",
                "短期的な利点",
                "長期的な上限",
                "コストと失う選択肢",
                "前提条件",
                "リスクと確認",
                "出典",
                "グループ能力の不足",
                "同意済みで表示可能な能力帯だけを要約し、非公開の仲間情報は表示しません。",
                "Chummer でプレビュー",
                "根拠付きプレビューを再構築",
                "Chummer の決定論的分析パケットを待っています。ルークはビルド事実を創作しません。",
                "分析パケットのスキーマ、ID、言語、リビジョン、またはダイジェストが無効です。安全なローカル代替を使用します。",
                "プレビューのみ利用可能",
                "プレビュー不可",
                "パケット結合"),
            ["pt-br"] = new(
                "Rook · Build Ghost",
                "Decker ork adulta. Rook explica apenas os fatos determinísticos fornecidos pelo Chummer.",
                "Build atual",
                "Orientação fundamentada",
                "Explicação de regra",
                "Comparar três builds",
                "Reparo conservador",
                "Especialização de função",
                "Híbrido equilibrado",
                "Benefício de curto prazo",
                "Potencial de longo prazo",
                "Custos e alternativas perdidas",
                "Dependências",
                "Risco e revisão",
                "Fontes",
                "Lacunas de capacidade do grupo",
                "Somente faixas de capacidade visíveis e consentidas são resumidas; detalhes ocultos da equipe nunca aparecem.",
                "Pré-visualizar no Chummer",
                "Reconstruir prévia fundamentada",
                "Aguardando o pacote de análise determinística do Chummer. Rook não inventará fatos do build.",
                "O esquema, a identidade, o idioma, a revisão ou o digest do pacote falhou na validação. Rook usa o fallback local seguro.",
                "Disponível somente para prévia",
                "Prévia indisponível",
                "Vínculo do pacote"),
            ["zh-cn"] = new(
                "Rook · Build Ghost",
                "成年女性兽人黑客。Rook 只解释 Chummer 提供的确定性事实。",
                "当前构筑",
                "有依据的建议",
                "规则说明",
                "比较三个构筑",
                "保守修补",
                "角色专精",
                "均衡混合",
                "短期收益",
                "长期上限",
                "成本与放弃的选择",
                "依赖条件",
                "风险与复核",
                "来源",
                "团队能力缺口",
                "只汇总已同意且可见的能力区间；绝不显示隐藏的队友详情。",
                "在 Chummer 中预览",
                "重新生成有依据的预览",
                "正在等待 Chummer 的确定性分析包。Rook 不会编造构筑事实。",
                "分析包的架构、身份、语言、修订或摘要验证失败。Rook 将使用安全的本地后备内容。",
                "仅可预览",
                "无法预览",
                "分析包绑定")
        };

    private static readonly IReadOnlyDictionary<string, string> DeterministicFallbackTexts =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en-us"] = "Rook is using Chummer's grounded local explanation.",
            ["de-de"] = "Rook verwendet Chummers belegte lokale Erklärung.",
            ["fr-fr"] = "Rook utilise l’explication locale fondée de Chummer.",
            ["ja-jp"] = "ルークは Chummer の根拠付きローカル説明を使用しています。",
            ["pt-br"] = "Rook está usando a explicação local fundamentada do Chummer.",
            ["zh-cn"] = "Rook 正在使用 Chummer 的有依据本地说明。"
        };

    internal static IReadOnlyList<string> MaterializedLocaleCodes { get; } = MaterializeLocaleCodes();

    internal static IReadOnlyList<string> SupportedContractLocales { get; } =
        MaterializedLocaleCodes.Select(ToContractLocale).ToArray();

    internal static string GetDeterministicFallbackText(string? requestedLocale)
        => DeterministicFallbackTexts[ResolveLocale(requestedLocale)];

    internal static IReadOnlyList<DesktopDialogField> CreateInterviewFields(string? requestedLocale)
    {
        string locale = ResolveLocale(requestedLocale);
        Copy copy = Copies[locale];
        return
        [
            Hidden(PacketFieldId, string.Empty),
            Hidden(LocaleFieldId, ToContractLocale(locale)),
            new DesktopDialogField(
                "autoAliceBuildGhostIdentity",
                copy.Title,
                $"{copy.PersonaSummary}{Environment.NewLine}persona | {PersonaId}{Environment.NewLine}avatar | {AvatarId}{Environment.NewLine}voice | {VoiceId}",
                copy.PersonaSummary,
                IsReadOnly: true,
                IsMultiline: true,
                VisualKind: DesktopDialogFieldVisualKinds.Summary,
                LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField(
                "autoAliceBuildGhostPortrait",
                copy.Title,
                PortraitAsset,
                PortraitAsset,
                IsReadOnly: true,
                VisualKind: DesktopDialogFieldVisualKinds.Image,
                LayoutSlot: DesktopDialogFieldLayoutSlots.Right)
        ];
    }

    internal static DesktopDialogState BindPacket(DesktopDialogState dialog, string packetJson)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentException.ThrowIfNullOrWhiteSpace(packetJson);
        return dialog with
        {
            Fields = ReplaceField(dialog.Fields, Hidden(PacketFieldId, packetJson))
        };
    }

    internal static IReadOnlyList<DesktopDialogField> AppendPreviewFields(
        IReadOnlyList<DesktopDialogField> existing,
        CharacterOverviewState state,
        out bool hasPreviewableVariant)
    {
        string locale = ResolveLocale(DesktopDialogFieldValueParser.GetValue(
            new DesktopDialogState(string.Empty, string.Empty, null, existing, []), LocaleFieldId));
        Copy copy = Copies[locale];
        List<DesktopDialogField> fields = existing
            .Where(static field => !field.Id.StartsWith("autoAliceBuildGhostPreview", StringComparison.Ordinal)
                && !string.Equals(field.Id, SelectedVariantFieldId, StringComparison.Ordinal)
                && !string.Equals(field.Id, PacketDigestFieldId, StringComparison.Ordinal))
            .ToList();
        string? packetJson = fields.FirstOrDefault(field => string.Equals(field.Id, PacketFieldId, StringComparison.Ordinal))?.Value;
        if (string.IsNullOrWhiteSpace(packetJson))
        {
            hasPreviewableVariant = false;
            fields.Add(StatusField(copy, copy.WaitingForPacket));
            return fields;
        }

        if (!TryParseValidatedPacket(packetJson, ToContractLocale(locale), state, out PacketProjection? packet, out string failure))
        {
            hasPreviewableVariant = false;
            fields.Add(StatusField(copy, $"{copy.InvalidPacket}{Environment.NewLine}validation | {failure}"));
            return fields;
        }

        fields.Add(Hidden(PacketDigestFieldId, packet.PacketDigest));
        fields.Add(new DesktopDialogField(
            "autoAliceBuildGhostPreviewBinding",
            copy.PacketBinding,
            $"schema | {AnalysisSchema}{Environment.NewLine}digest | {packet.PacketDigest}{Environment.NewLine}revision | {packet.WorkspaceRevision}{Environment.NewLine}source | {packet.SourceDigest}{Environment.NewLine}locale | {packet.Locale}",
            packet.PacketDigest,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.Snippet));
        fields.Add(new DesktopDialogField(
            "autoAliceBuildGhostPreviewFacts",
            copy.CurrentBuild,
            RenderFacts(packet),
            copy.CurrentBuild,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.Grid,
            LayoutSlot: DesktopDialogFieldLayoutSlots.Left));
        fields.Add(new DesktopDialogField(
            "autoAliceBuildGhostPreviewAdvice",
            copy.Advice,
            RenderAdvice(packet, copy),
            copy.Advice,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.List,
            LayoutSlot: DesktopDialogFieldLayoutSlots.Right));
        fields.Add(new DesktopDialogField(
            "autoAliceBuildGhostPreviewRules",
            copy.Rules,
            RenderRules(packet),
            copy.Rules,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.Book));

        foreach (VariantProjection variant in packet.Variants)
        {
            fields.Add(new DesktopDialogField(
                $"autoAliceBuildGhostPreviewVariant_{variant.Shape}",
                ShapeLabel(copy, variant.Shape),
                RenderVariant(variant, copy),
                ShapeLabel(copy, variant.Shape),
                IsReadOnly: true,
                IsMultiline: true,
                VisualKind: DesktopDialogFieldVisualKinds.Detail));
        }

        fields.Add(new DesktopDialogField(
            "autoAliceBuildGhostPreviewGroup",
            copy.Group,
            RenderGroup(packet, copy),
            copy.GroupPrivacy,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.Snippet));

        VariantProjection[] previewable = packet.Variants.Where(static variant => variant.Previewable).ToArray();
        hasPreviewableVariant = previewable.Length > 0;
        fields.Add(new DesktopDialogField(
            SelectedVariantFieldId,
            copy.Compare,
            previewable.FirstOrDefault()?.VariantId ?? string.Empty,
            previewable.FirstOrDefault()?.VariantId ?? string.Empty,
            IsReadOnly: previewable.Length == 0,
            InputType: previewable.Length == 0 ? "text" : "select",
            Options: previewable.Select(variant => new DesktopDialogFieldOption(variant.VariantId, ShapeLabel(copy, variant.Shape))).ToArray()));
        return fields;
    }

    internal static string PreviewActionLabel(DesktopDialogState dialog)
    {
        string locale = ResolveLocale(DesktopDialogFieldValueParser.GetValue(dialog, LocaleFieldId));
        return Copies[locale].Preview;
    }

    internal static bool TryCreatePreviewReceipt(
        DesktopDialogState dialog,
        CharacterOverviewState state,
        out string receipt,
        out string error)
    {
        receipt = string.Empty;
        error = string.Empty;
        string locale = ResolveLocale(DesktopDialogFieldValueParser.GetValue(dialog, LocaleFieldId));
        string? packetJson = DesktopDialogFieldValueParser.GetValue(dialog, PacketFieldId);
        if (!TryParseValidatedPacket(packetJson, ToContractLocale(locale), state, out PacketProjection? packet, out error))
        {
            return false;
        }

        string? selected = DesktopDialogFieldValueParser.GetValue(dialog, SelectedVariantFieldId);
        VariantProjection? variant = packet.Variants.FirstOrDefault(candidate =>
            candidate.Previewable && string.Equals(candidate.VariantId, selected, StringComparison.Ordinal));
        if (variant is null)
        {
            error = "build-ghost-variant-not-previewable";
            return false;
        }

        receipt = $"{Copies[locale].Preview}: {ShapeLabel(Copies[locale], variant.Shape)} · variant={variant.VariantId} · packet={packet.PacketDigest} · input={packet.InputDigest} · source={packet.SourceDigest} · revision={packet.WorkspaceRevision}. No dossier mutation was performed.";
        return true;
    }

    internal static string ComputePacketDigest(JsonNode packet)
    {
        JsonNode clone = packet.DeepClone();
        if (clone is not JsonObject root)
        {
            throw new ArgumentException("Build Ghost packet must be a JSON object.", nameof(packet));
        }

        string digestProperty = root.Select(static pair => pair.Key)
            .FirstOrDefault(static key => string.Equals(key, "packetDigest", StringComparison.OrdinalIgnoreCase))
            ?? "packetDigest";
        root[digestProperty] = string.Empty;
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            WriteCanonical(writer, root);
        }

        return $"sha256:{Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant()}";
    }

    private static bool TryParseValidatedPacket(
        string? json,
        string expectedLocale,
        CharacterOverviewState state,
        [NotNullWhen(true)] out PacketProjection? projection,
        out string failure)
    {
        projection = null;
        failure = "build-ghost-packet-missing";
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            JsonNode? node = JsonNode.Parse(json);
            if (node is not JsonObject root)
            {
                failure = "build-ghost-packet-not-object";
                return false;
            }

            string schema = Text(root, "schema");
            string persona = Text(root, "personaId");
            string avatar = Text(root, "avatarId");
            string voice = Text(root, "voiceId");
            string locale = Text(root, "locale");
            string packetDigest = Text(root, "packetDigest");
            string inputDigest = Text(root, "inputDigest");
            string sourceDigest = Text(root, "sourceDigest");
            string workspaceId = Text(root, "workspaceId");
            long workspaceRevision = Number(root, "workspaceRevision");
            if (!string.Equals(schema, AnalysisSchema, StringComparison.Ordinal)
                || !string.Equals(persona, PersonaId, StringComparison.Ordinal)
                || !string.Equals(avatar, AvatarId, StringComparison.Ordinal)
                || !string.Equals(voice, VoiceId, StringComparison.Ordinal))
            {
                failure = "build-ghost-packet-contract-mismatch";
                return false;
            }

            if (!string.Equals(locale, expectedLocale, StringComparison.OrdinalIgnoreCase))
            {
                failure = "build-ghost-packet-locale-mismatch";
                return false;
            }

            if (!string.Equals(packetDigest, ComputePacketDigest(root), StringComparison.Ordinal))
            {
                failure = "build-ghost-packet-digest-mismatch";
                return false;
            }

            if (!IsSha256(packetDigest) || !IsSha256(inputDigest) || !IsSha256(sourceDigest))
            {
                failure = "build-ghost-packet-digest-shape-mismatch";
                return false;
            }

            if (state.WorkspaceId is null
                || !string.Equals(workspaceId, state.WorkspaceId.ToString(), StringComparison.Ordinal)
                || workspaceRevision != state.ContentRevision)
            {
                failure = "build-ghost-packet-workspace-revision-mismatch";
                return false;
            }

            JsonObject[] allowedActions = Array(root, "allowedSuggestedActions").OfType<JsonObject>().ToArray();
            VariantProjection[] variants = Array(root, "variants")
                .OfType<JsonObject>()
                .Select(variant => ParseVariant(
                    variant,
                    workspaceRevision,
                    sourceDigest,
                    inputDigest,
                    allowedActions))
                .ToArray();
            string[] requiredShapes = ["conservative-repair", "role-focused-specialization", "balanced-hybrid"];
            if (variants.Length != 3 || !requiredShapes.SequenceEqual(variants.Select(static variant => variant.Shape), StringComparer.Ordinal))
            {
                failure = "build-ghost-packet-variant-shape-mismatch";
                return false;
            }

            projection = new PacketProjection(
                packetDigest,
                inputDigest,
                locale,
                workspaceRevision,
                sourceDigest,
                Array(root, "strengths").OfType<JsonObject>().ToArray(),
                Array(root, "blockers").OfType<JsonObject>().ToArray(),
                Array(root, "warnings").OfType<JsonObject>().ToArray(),
                Array(root, "tips").OfType<JsonObject>().ToArray(),
                Array(root, "ruleExplanations").OfType<JsonObject>().ToArray(),
                variants,
                Object(root, "groupCapabilityPosture"));
            failure = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            failure = "build-ghost-packet-invalid-json";
            return false;
        }
    }

    private static VariantProjection ParseVariant(
        JsonObject value,
        long workspaceRevision,
        string sourceDigest,
        string inputDigest,
        IReadOnlyList<JsonObject> allowedActions)
    {
        JsonObject? validation = Object(value, "validation");
        JsonObject? applyPreview = Object(value, "applyPreview");
        string variantId = Text(value, "variantId");
        string actionId = Text(applyPreview, "actionId");
        bool bindingValid = !string.IsNullOrWhiteSpace(actionId)
            && string.Equals(Text(value, "inputDigest"), inputDigest, StringComparison.Ordinal)
            && string.Equals(Text(applyPreview, "actionType"), PreviewBuildVariantActionType, StringComparison.Ordinal)
            && string.Equals(Text(applyPreview, "variantId"), variantId, StringComparison.Ordinal)
            && Number(applyPreview, "expectedWorkspaceRevision") == workspaceRevision
            && string.Equals(Text(applyPreview, "expectedSourceDigest"), sourceDigest, StringComparison.Ordinal)
            && string.Equals(Text(applyPreview, "expectedInputDigest"), inputDigest, StringComparison.Ordinal)
            && allowedActions.Any(action =>
                string.Equals(Text(action, "actionId"), actionId, StringComparison.Ordinal)
                && string.Equals(Text(action, "actionType"), PreviewBuildVariantActionType, StringComparison.Ordinal)
                && string.Equals(Text(action, "variantId"), variantId, StringComparison.Ordinal)
                && Boolean(action, "requiresExplicitReview")
                && Number(action, "workspaceRevision") == workspaceRevision
                && string.Equals(Text(action, "sourceDigest"), sourceDigest, StringComparison.Ordinal));
        bool declaredAvailable = string.Equals(Text(validation, "status"), "available", StringComparison.Ordinal);
        bool previewable = declaredAvailable
            && Boolean(applyPreview, "previewOnly")
            && Boolean(applyPreview, "requiresExplicitReview")
            && bindingValid;
        string[] blockers = Strings(validation, "blockers")
            .Concat(declaredAvailable && !bindingValid ? ["build-ghost-preview-binding-mismatch"] : [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new VariantProjection(
            variantId,
            Text(value, "shape"),
            Text(value, "shortTermBenefit"),
            Text(value, "longTermCeiling"),
            Strings(value, "costsAndLostAlternatives"),
            Strings(value, "dependencies"),
            Strings(value, "gmPolicyConflicts"),
            blockers,
            Strings(validation, "warnings"),
            previewable);
    }

    private static bool IsSha256(string? value)
        => value is { Length: 71 }
            && value.StartsWith("sha256:", StringComparison.Ordinal)
            && value.Skip(7).All(Uri.IsHexDigit);

    private static string RenderFacts(PacketProjection packet)
    {
        IEnumerable<JsonObject> facts = packet.Strengths.Concat(packet.Blockers).Concat(packet.Warnings);
        string[] lines = facts.Select(fact =>
        {
            string anchors = string.Join(", ", Strings(fact, "sourceAnchorIds"));
            return $"{Text(fact, "label")} | {Text(fact, "value")} | {anchors}";
        }).ToArray();
        return lines.Length == 0 ? "none" : string.Join(Environment.NewLine, lines);
    }

    private static string RenderAdvice(PacketProjection packet, Copy copy)
    {
        string[] lines = packet.Tips.Select(tip =>
            $"{Text(tip, "explanation")}{Environment.NewLine}{copy.ShortTerm} | {Text(tip, "expectedBenefit")}{Environment.NewLine}{copy.Costs} | {Text(tip, "opportunityCost")}{Environment.NewLine}{copy.Risk} | {Text(tip, "risk")}{Environment.NewLine}{copy.Sources} | {string.Join(", ", Strings(tip, "sourceAnchorIds"))}").ToArray();
        return lines.Length == 0 ? "none" : string.Join($"{Environment.NewLine}{Environment.NewLine}", lines);
    }

    private static string RenderRules(PacketProjection packet)
    {
        string[] lines = packet.Rules.Select(rule =>
            $"{Text(rule, "question")}{Environment.NewLine}{Text(rule, "status")} | {Text(rule, "explanation")}{Environment.NewLine}source | {string.Join(", ", Strings(rule, "sourceAnchorIds"))}").ToArray();
        return lines.Length == 0 ? "none" : string.Join($"{Environment.NewLine}{Environment.NewLine}", lines);
    }

    private static string RenderVariant(VariantProjection variant, Copy copy)
        => $"{copy.ShortTerm} | {variant.ShortTermBenefit}{Environment.NewLine}" +
           $"{copy.LongTerm} | {variant.LongTermCeiling}{Environment.NewLine}" +
           $"{copy.Costs} | {JoinBuildGhostValues(variant.Costs)}{Environment.NewLine}" +
           $"{copy.Dependencies} | {JoinBuildGhostValues(variant.Dependencies)}{Environment.NewLine}" +
           $"{copy.Risk} | {JoinBuildGhostValues(variant.GmConflicts.Concat(variant.Blockers).Concat(variant.Warnings))}{Environment.NewLine}" +
           (variant.Previewable ? copy.Available : copy.Unavailable);

    private static string RenderGroup(PacketProjection packet, Copy copy)
    {
        if (packet.Group is null)
        {
            return copy.GroupPrivacy;
        }

        string posture = Text(packet.Group, "visibilityPosture");
        string[] conclusions = Array(packet.Group, "conclusions").OfType<JsonObject>()
            .Select(static conclusion => Text(conclusion, "wording"))
            .Where(static wording => !string.IsNullOrWhiteSpace(wording))
            .ToArray();
        return $"{copy.GroupPrivacy}{Environment.NewLine}visibility | {posture}{Environment.NewLine}{string.Join(Environment.NewLine, conclusions)}";
    }

    private static DesktopDialogField StatusField(Copy copy, string value)
        => new(
            "autoAliceBuildGhostPreviewStatus",
            copy.Title,
            value,
            value,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.Snippet);

    private static string ShapeLabel(Copy copy, string shape)
        => shape switch
        {
            "conservative-repair" => copy.Conservative,
            "role-focused-specialization" => copy.Focused,
            "balanced-hybrid" => copy.Balanced,
            _ => shape
        };

    private static IReadOnlyList<DesktopDialogField> ReplaceField(
        IReadOnlyList<DesktopDialogField> fields,
        DesktopDialogField replacement)
        => fields.Where(field => !string.Equals(field.Id, replacement.Id, StringComparison.Ordinal))
            .Append(replacement)
            .ToArray();

    private static DesktopDialogField Hidden(string id, string value)
        => new(id, id, value, value, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden);

    private static string ResolveLocale(string? requestedLocale)
    {
        if (string.IsNullOrWhiteSpace(requestedLocale))
        {
            return DesktopLocalizationCatalog.DefaultLanguage;
        }

        string normalized = requestedLocale.Trim().ToLowerInvariant();
        if (!MaterializedLocaleCodes.Contains(normalized, StringComparer.Ordinal))
        {
            throw new NotSupportedException($"build-ghost-locale-not-materialized:{requestedLocale}");
        }

        return normalized;
    }

    private static string ToContractLocale(string locale)
    {
        if (!MaterializedLocaleCodes.Contains(locale, StringComparer.Ordinal))
        {
            throw new NotSupportedException($"build-ghost-locale-not-materialized:{locale}");
        }

        return CultureInfo.GetCultureInfo(locale).Name;
    }

    private static IReadOnlyList<string> MaterializeLocaleCodes()
    {
        string[] canonical = DesktopLocalizationCatalog.ShippingLanguages
            .Select(static language => language.Code)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static code => code, StringComparer.Ordinal)
            .ToArray();
        string[] copies = Copies.Keys.OrderBy(static code => code, StringComparer.Ordinal).ToArray();
        if (!canonical.SequenceEqual(copies, StringComparer.Ordinal))
        {
            string missing = string.Join(",", canonical.Except(copies, StringComparer.Ordinal));
            string extra = string.Join(",", copies.Except(canonical, StringComparer.Ordinal));
            throw new InvalidOperationException($"build-ghost-locale-coverage-drift:missing={missing};extra={extra}");
        }

        return canonical;
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                return;
            case JsonObject value:
                writer.WriteStartObject();
                foreach ((string name, JsonNode? child) in value.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(name);
                    WriteCanonical(writer, child);
                }
                writer.WriteEndObject();
                return;
            case JsonArray value:
                writer.WriteStartArray();
                foreach (JsonNode? child in value)
                {
                    WriteCanonical(writer, child);
                }
                writer.WriteEndArray();
                return;
            default:
                node.WriteTo(writer);
                return;
        }
    }

    private static string Text(JsonObject? value, string property)
        => Find(value, property)?.GetValue<string>() ?? string.Empty;

    private static long Number(JsonObject? value, string property)
        => Find(value, property)?.GetValue<long>() ?? 0;

    private static bool Boolean(JsonObject? value, string property)
        => Find(value, property)?.GetValue<bool>() ?? false;

    private static JsonObject? Object(JsonObject? value, string property)
        => Find(value, property) as JsonObject;

    private static JsonArray Array(JsonObject? value, string property)
        => Find(value, property) as JsonArray ?? [];

    private static IReadOnlyList<string> Strings(JsonObject? value, string property)
        => Array(value, property).Select(static item => item?.GetValue<string>() ?? string.Empty)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

    private static JsonNode? Find(JsonObject? value, string property)
        => value?.FirstOrDefault(pair => string.Equals(pair.Key, property, StringComparison.OrdinalIgnoreCase)).Value;

    private static string JoinBuildGhostValues(IEnumerable<string> values)
    {
        string[] materialized = values.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return materialized.Length == 0 ? "none" : string.Join("; ", materialized);
    }

    private sealed record PacketProjection(
        string PacketDigest,
        string InputDigest,
        string Locale,
        long WorkspaceRevision,
        string SourceDigest,
        IReadOnlyList<JsonObject> Strengths,
        IReadOnlyList<JsonObject> Blockers,
        IReadOnlyList<JsonObject> Warnings,
        IReadOnlyList<JsonObject> Tips,
        IReadOnlyList<JsonObject> Rules,
        IReadOnlyList<VariantProjection> Variants,
        JsonObject? Group);

    private sealed record VariantProjection(
        string VariantId,
        string Shape,
        string ShortTermBenefit,
        string LongTermCeiling,
        IReadOnlyList<string> Costs,
        IReadOnlyList<string> Dependencies,
        IReadOnlyList<string> GmConflicts,
        IReadOnlyList<string> Blockers,
        IReadOnlyList<string> Warnings,
        bool Previewable);

    private sealed record Copy(
        string Title,
        string PersonaSummary,
        string CurrentBuild,
        string Advice,
        string Rules,
        string Compare,
        string Conservative,
        string Focused,
        string Balanced,
        string ShortTerm,
        string LongTerm,
        string Costs,
        string Dependencies,
        string Risk,
        string Sources,
        string Group,
        string GroupPrivacy,
        string Preview,
        string Rebuild,
        string WaitingForPacket,
        string InvalidPacket,
        string Available,
        string Unavailable,
        string PacketBinding);
}
