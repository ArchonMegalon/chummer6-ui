using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

internal static class DesktopAliceAssistant
{
    internal const string CommandId = "auto_alice";
    internal const string DialogId = "dialog.auto_alice";
    internal const string PreviewActionId = "preview_auto_alice";
    internal const string ApplyActionId = "apply_auto_alice";
    internal const string OpenHandoffActionId = "open_auto_alice_handoff";

    private const string SurfaceIdFieldId = "autoAliceSurfaceId";
    private const string SurfaceLabelFieldId = "autoAliceSurfaceLabel";
    private const string SupportModeFieldId = "autoAliceSupportMode";
    private const string HandoffCommandFieldId = "autoAliceHandoffCommandId";
    private const string ConversationModeFieldId = "autoAliceConversationMode";
    private const string RulesetFieldId = "autoAliceRulesetId";
    private const string WorkspaceFieldId = "autoAliceWorkspaceId";
    private const string ArchetypeFieldId = "autoAliceArchetype";
    private const string OptimizationFieldId = "autoAliceOptimization";
    private const string LegalityFieldId = "autoAliceLegality";
    private const string ComplexityFieldId = "autoAliceComplexity";
    private const string GmRequirementsFieldId = "autoAliceGmRequirements";

    private const string BuildHelpConversationMode = "build_help";
    private const string RulesCoachConversationMode = "rules_coach";
    private const string OriginDossierConversationMode = "origin_dossier";

    public static DesktopDialogState CreateDialog(
        string? activeSectionId,
        string? activeDialogId,
        string? activeSectionJson,
        CharacterWorkspaceId? currentWorkspace,
        string? rulesetId)
    {
        AliceSurfacePlan plan = ResolvePlan(activeSectionId, activeDialogId);
        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        string workspaceId = currentWorkspace?.Value ?? string.Empty;
        bool blankState = currentWorkspace is null
            && string.IsNullOrWhiteSpace(activeSectionId)
            && string.IsNullOrWhiteSpace(activeDialogId);
        string message = blankState
            ? "No runner is open yet. ALICE can plan a complete first build, explain the settings, or start an origin dossier. Use the guided workflow handoff when ready."
            : plan.SupportMode switch
        {
            AliceSupportMode.QuickAddApply => IsPreviewRuleset(normalizedRulesetId)
                ? $"ALICE can propose one scoped SR4 preview change for {plan.SurfaceLabel.ToLowerInvariant()}. Answer the questions, preview the result, then review it before you apply."
                : $"ALICE can propose one scoped change for {plan.SurfaceLabel.ToLowerInvariant()}. Answer the questions, preview the result, then apply it explicitly.",
            AliceSupportMode.GuidedBuildPlan => IsPreviewRuleset(normalizedRulesetId)
                ? "ALICE can shape an SR4 preview build, but this frame still needs the explicit BP or Karma workflow handoff instead of silent mutation."
                : "ALICE can shape the current build, but this frame still needs a guided workflow handoff instead of silent mutation.",
            AliceSupportMode.SettingsHandoff => IsPreviewRuleset(normalizedRulesetId)
                ? "ALICE can suggest an SR4-safe settings posture here, then hand you to the real settings form for deliberate edits."
                : "ALICE can suggest a sane settings posture here, then hand you to the real settings form for deliberate edits.",
            _ => "No runner is open yet. ALICE can still plan a complete first build, explain the settings, or start an origin dossier."
        };

        return new DesktopDialogState(
            DialogId,
            "Auto ALICE",
            message,
            BuildInterviewFields(plan, normalizedRulesetId, workspaceId),
            BuildInterviewActions());
    }

    public static DesktopDialogState BuildPreviewDialog(DesktopDialogState dialog, CharacterOverviewState state)
    {
        AliceSurfacePlan plan = ResolvePlanFromDialog(dialog, state.ActiveSectionId, state.ActiveDialog?.Id);
        AliceProposal proposal = BuildProposal(dialog, plan);

        return dialog with
        {
            Message = proposal.Message,
            Fields = BuildPreviewFields(dialog.Fields, proposal),
            Actions = BuildPreviewActions(proposal)
        };
    }

    public static bool TryBuildQuickAddRequest(
        DesktopDialogState dialog,
        CharacterOverviewState state,
        out WorkspaceQuickAddRequest request,
        out string notice)
    {
        AliceSurfacePlan plan = ResolvePlanFromDialog(dialog, state.ActiveSectionId, state.ActiveDialog?.Id);
        AliceProposal proposal = BuildProposal(dialog, plan);
        if (proposal.Request is null)
        {
            request = default!;
            notice = string.Empty;
            return false;
        }

        request = proposal.Request;
        notice = proposal.ApplyNotice;
        return true;
    }

    public static string? TryGetHandoffCommandId(DesktopDialogState dialog, CharacterOverviewState state)
    {
        AliceSurfacePlan plan = ResolvePlanFromDialog(dialog, state.ActiveSectionId, state.ActiveDialog?.Id);
        AliceProposal proposal = BuildProposal(dialog, plan);
        if (!string.IsNullOrWhiteSpace(proposal.HandoffCommandId))
        {
            return proposal.HandoffCommandId;
        }

        string? stored = DesktopDialogFieldValueParser.GetValue(dialog, HandoffCommandFieldId);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        return plan.HandoffCommandId;
    }

    private static IReadOnlyList<DesktopDialogField> BuildInterviewFields(
        AliceSurfacePlan plan,
        string rulesetId,
        string workspaceId)
    {
        return
        [
            HiddenField(SurfaceIdFieldId, plan.SurfaceId),
            HiddenField(SurfaceLabelFieldId, plan.SurfaceLabel),
            HiddenField(SupportModeFieldId, plan.SupportMode.ToString()),
            HiddenField(HandoffCommandFieldId, plan.HandoffCommandId ?? string.Empty),
            HiddenField(RulesetFieldId, rulesetId),
            HiddenField(WorkspaceFieldId, workspaceId),
            new DesktopDialogField(
                ConversationModeFieldId,
                "Mode",
                BuildHelpConversationMode,
                BuildHelpConversationMode,
                InputType: "select",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                Options:
                [
                    new(BuildHelpConversationMode, "Build help"),
                    new(RulesCoachConversationMode, "Rules coach"),
                    new(OriginDossierConversationMode, "Origin Dossier")
                ]),
            new DesktopDialogField(
                "autoAliceSurfaceContext",
                "Current Surface",
                $"Surface | {plan.SurfaceLabel}{Environment.NewLine}Support | {plan.SupportLabel}{Environment.NewLine}Posture | {plan.SurfaceSummary}",
                plan.SurfaceLabel,
                IsReadOnly: true,
                IsMultiline: true,
                VisualKind: DesktopDialogFieldVisualKinds.Grid,
                LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField(
                "autoAliceQuestionPrompt",
                "How This Works",
                "Choose build help, rules coach, or origin dossier. ALICE can work before a character exists, and finished characters are not changed unless you apply a separate explicit edit.",
                "Choose a mode first.",
                IsReadOnly: true,
                IsMultiline: true,
                VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField(
                "autoAliceSettingsGuide",
                "Settings Guide",
                "Strict avoids restricted picks. Standard allows common legal restricted choices. Anything includes table exceptions and always needs review. Simple keeps the path obvious, Standard balances depth, and Deep explores tighter tradeoffs. Ware advice calls out essence, nuyen, legality, and recovery risk before anything is applied.",
                "Strict vs Standard explanation",
                IsReadOnly: true,
                IsMultiline: true,
                VisualKind: DesktopDialogFieldVisualKinds.Snippet,
                LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField(
                ArchetypeFieldId,
                "Archetype",
                "street_sam",
                "street_sam",
                InputType: "select",
                Options:
                [
                    new("street_sam", "Street Sam"),
                    new("decker", "Decker"),
                    new("mage", "Mage"),
                    new("face", "Face"),
                    new("rigger", "Rigger"),
                    new("adept", "Adept"),
                    new("generalist", "Generalist")
                ]),
            new DesktopDialogField(
                OptimizationFieldId,
                "Optimization Bias",
                "balanced",
                "balanced",
                InputType: "select",
                Options:
                [
                    new("balanced", "Balanced"),
                    new("specialized", "Specialized"),
                    new("survivable", "Survivable"),
                    new("cheap", "Cheap")
                ]),
            new DesktopDialogField(
                LegalityFieldId,
                "Legality",
                "strict",
                "strict",
                InputType: "select",
                Options:
                [
                    new("strict", "Strict"),
                    new("standard", "Standard"),
                    new("anything", "Anything")
                ]),
            new DesktopDialogField(
                ComplexityFieldId,
                "Complexity",
                "standard",
                "standard",
                InputType: "select",
                Options:
                [
                    new("simple", "Simple"),
                    new("standard", "Standard"),
                    new("deep", "Deep")
                ]),
            new DesktopDialogField(
                GmRequirementsFieldId,
                "GM Requirements / Grants",
                string.Empty,
                "Optional: must be magically active, must have Intelligence 2+, must have an illegal addiction, bonus nuyen, extra quality, availability or ware exception, required gear, banned choices.",
                IsMultiline: true,
                LayoutSlot: DesktopDialogFieldLayoutSlots.Right)
        ];
    }

    private static IReadOnlyList<DesktopDialogAction> BuildInterviewActions()
        =>
        [
            new DesktopDialogAction(PreviewActionId, "Preview proposal", true),
            new DesktopDialogAction("cancel", "Cancel")
        ];

    private static IReadOnlyList<DesktopDialogField> BuildPreviewFields(
        IReadOnlyList<DesktopDialogField> existingFields,
        AliceProposal proposal)
    {
        List<DesktopDialogField> fields = new(existingFields.Count + 3);
        fields.AddRange(existingFields.Where(field =>
            !string.Equals(field.Id, "autoAliceProposalSummary", StringComparison.Ordinal)
            && !string.Equals(field.Id, "autoAliceProposalChanges", StringComparison.Ordinal)
            && !string.Equals(field.Id, "autoAliceProposalWarnings", StringComparison.Ordinal)));
        fields.Add(new DesktopDialogField(
            "autoAliceProposalSummary",
            "Proposal Summary",
            proposal.Summary,
            proposal.Summary,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.Snippet));
        fields.Add(new DesktopDialogField(
            "autoAliceProposalChanges",
            "Suggested Changes",
            proposal.ChangeList,
            proposal.ChangeList,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.List,
            LayoutSlot: DesktopDialogFieldLayoutSlots.Right));
        fields.Add(new DesktopDialogField(
            "autoAliceProposalWarnings",
            "Warnings",
            proposal.WarningList,
            proposal.WarningList,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.Snippet,
            LayoutSlot: DesktopDialogFieldLayoutSlots.Right));
        return fields;
    }

    private static IReadOnlyList<DesktopDialogAction> BuildPreviewActions(AliceProposal proposal)
    {
        List<DesktopDialogAction> actions = [new DesktopDialogAction(PreviewActionId, "Rebuild preview")];
        if (proposal.Request is not null)
        {
            actions.Insert(0, new DesktopDialogAction(ApplyActionId, "Apply proposal", true));
        }
        else if (!string.IsNullOrWhiteSpace(proposal.HandoffCommandId))
        {
            actions.Insert(0, new DesktopDialogAction(OpenHandoffActionId, proposal.HandoffLabel, true));
        }

        actions.Add(new DesktopDialogAction("cancel", "Cancel"));
        return actions;
    }

    private static DesktopDialogField HiddenField(string id, string value)
        => new(id, id, value, value, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden);

    private static AliceSurfacePlan ResolvePlanFromDialog(
        DesktopDialogState dialog,
        string? fallbackSectionId,
        string? fallbackDialogId)
    {
        string? surfaceId = DesktopDialogFieldValueParser.GetValue(dialog, SurfaceIdFieldId);
        string? surfaceLabel = DesktopDialogFieldValueParser.GetValue(dialog, SurfaceLabelFieldId);
        string? supportMode = DesktopDialogFieldValueParser.GetValue(dialog, SupportModeFieldId);
        string? handoffCommandId = DesktopDialogFieldValueParser.GetValue(dialog, HandoffCommandFieldId);
        if (!string.IsNullOrWhiteSpace(surfaceId)
            && !string.IsNullOrWhiteSpace(surfaceLabel)
            && Enum.TryParse(supportMode, ignoreCase: true, out AliceSupportMode parsedMode))
        {
            return new AliceSurfacePlan(
                surfaceId,
                surfaceLabel,
                parsedMode,
                ResolveSupportLabel(parsedMode),
                ResolveSurfaceSummary(surfaceId, parsedMode),
                handoffCommandId);
        }

        return ResolvePlan(fallbackSectionId, fallbackDialogId);
    }

    private static AliceSurfacePlan ResolvePlan(string? activeSectionId, string? activeDialogId)
    {
        string? normalizedSection = NormalizeSurfaceId(activeSectionId);
        if (string.Equals(activeDialogId, "dialog.global_settings", StringComparison.Ordinal))
        {
            return BuildPlan("global_settings", "Global Settings", AliceSupportMode.SettingsHandoff, "global_settings");
        }

        if (string.Equals(activeDialogId, "dialog.character_settings", StringComparison.Ordinal))
        {
            return BuildPlan("character_settings", "Character Settings", AliceSupportMode.SettingsHandoff, "character_settings");
        }

        if (string.Equals(activeDialogId, "dialog.new_character", StringComparison.Ordinal)
            || string.Equals(activeDialogId, "dialog.new_character.priority_workflow", StringComparison.Ordinal)
            || string.Equals(activeDialogId, "dialog.new_character.karma_workflow", StringComparison.Ordinal))
        {
            return BuildPlan("character_create", "Character Create", AliceSupportMode.GuidedBuildPlan, "new_character");
        }

        return normalizedSection switch
        {
            "gear" or "inventory" or "gearlocations" => BuildPlan("gear", "Gear", AliceSupportMode.QuickAddApply),
            "weapons" or "weaponaccessories" or "weaponlocations" => BuildPlan("weapons", "Weapons", AliceSupportMode.QuickAddApply),
            "armors" or "armormods" or "armorlocations" => BuildPlan("armors", "Armor", AliceSupportMode.QuickAddApply),
            "cyberwares" => BuildPlan("cyberwares", "Cyberware", AliceSupportMode.QuickAddApply),
            "drugs" => BuildPlan("drugs", "Drugs", AliceSupportMode.QuickAddApply),
            "spells" => BuildPlan("spells", "Spells", AliceSupportMode.QuickAddApply),
            "powers" => BuildPlan("powers", "Adept Powers", AliceSupportMode.QuickAddApply),
            "complexforms" => BuildPlan("complexforms", "Complex Forms", AliceSupportMode.QuickAddApply),
            "initiationgrades" => BuildPlan("initiationgrades", "Initiation / Submersion", AliceSupportMode.QuickAddApply),
            "spirits" => BuildPlan("spirits", "Spirits", AliceSupportMode.QuickAddApply),
            "critterpowers" => BuildPlan("critterpowers", "Critter Powers", AliceSupportMode.QuickAddApply),
            "aiprograms" => BuildPlan("aiprograms", "Programs", AliceSupportMode.QuickAddApply),
            "vehicles" => BuildPlan("vehicles", "Vehicles", AliceSupportMode.QuickAddApply),
            "contacts" => BuildPlan("contacts", "Contacts", AliceSupportMode.QuickAddApply),
            "skills" => BuildPlan("skills", "Skills", AliceSupportMode.QuickAddApply),
            "qualities" => BuildPlan("qualities", "Qualities", AliceSupportMode.QuickAddApply),
            "create" or "metatype" or "priority" => BuildPlan("character_create", "Character Create", AliceSupportMode.GuidedBuildPlan, "new_character"),
            "settings" => BuildPlan("character_settings", "Character Settings", AliceSupportMode.SettingsHandoff, "character_settings"),
            _ => BuildPlan("character_create", "New Character", AliceSupportMode.GuidedBuildPlan, "new_character")
        };
    }

    private static AliceSurfacePlan BuildPlan(string surfaceId, string label, AliceSupportMode mode, string? handoffCommandId = null)
        => new(
            surfaceId,
            label,
            mode,
            ResolveSupportLabel(mode),
            ResolveSurfaceSummary(surfaceId, mode),
            handoffCommandId);

    private static string ResolveSupportLabel(AliceSupportMode mode)
        => mode switch
        {
            AliceSupportMode.QuickAddApply => "Scoped apply",
            AliceSupportMode.GuidedBuildPlan => "Guided handoff",
            AliceSupportMode.SettingsHandoff => "Settings handoff",
            _ => "Build from scratch"
        };

    private static string ResolveSurfaceSummary(string surfaceId, AliceSupportMode mode)
        => mode switch
        {
            AliceSupportMode.QuickAddApply => $"ALICE can add one reasonable {surfaceId.Replace('_', ' ')} proposal on first-party rails.",
            AliceSupportMode.GuidedBuildPlan => "ALICE can recommend a build direction, but the create surface still needs the named workflow.",
            AliceSupportMode.SettingsHandoff => "ALICE can suggest defaults, but the settings lane still owns the actual edits.",
            _ => "ALICE can start from a blank table, explain settings, or open the origin dossier path."
        };

    private static string? NormalizeSurfaceId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Contains("gear", StringComparison.Ordinal))
        {
            return "gear";
        }

        if (normalized.Contains("create", StringComparison.Ordinal)
            || normalized.Contains("metatype", StringComparison.Ordinal)
            || normalized.Contains("priority", StringComparison.Ordinal))
        {
            return "create";
        }

        if (normalized.Contains("settings", StringComparison.Ordinal))
        {
            return "settings";
        }

        return normalized;
    }

    private static AliceProposal BuildProposal(DesktopDialogState dialog, AliceSurfacePlan plan)
    {
        string archetype = DesktopDialogFieldValueParser.GetValue(dialog, ArchetypeFieldId) ?? "street_sam";
        string optimization = DesktopDialogFieldValueParser.GetValue(dialog, OptimizationFieldId) ?? "balanced";
        string legality = DesktopDialogFieldValueParser.GetValue(dialog, LegalityFieldId) ?? "strict";
        string complexity = DesktopDialogFieldValueParser.GetValue(dialog, ComplexityFieldId) ?? "standard";
        string conversationMode = NormalizeConversationMode(DesktopDialogFieldValueParser.GetValue(dialog, ConversationModeFieldId));
        string gmRequirements = DesktopDialogFieldValueParser.GetValue(dialog, GmRequirementsFieldId) ?? string.Empty;

        string rulesetId = RulesetDefaults.NormalizeOptional(DesktopDialogFieldValueParser.GetValue(dialog, RulesetFieldId)) ?? RulesetDefaults.Sr5;

        if (string.Equals(conversationMode, OriginDossierConversationMode, StringComparison.Ordinal))
        {
            return BuildOriginDossierProposal(rulesetId, archetype, optimization, legality, complexity, gmRequirements);
        }

        if (string.Equals(conversationMode, RulesCoachConversationMode, StringComparison.Ordinal))
        {
            return BuildRulesCoachProposal(rulesetId, archetype, optimization, legality, complexity, gmRequirements);
        }

        return plan.SupportMode switch
        {
            AliceSupportMode.QuickAddApply => BuildQuickAddProposal(plan, rulesetId, archetype, optimization, legality, complexity, gmRequirements),
            AliceSupportMode.GuidedBuildPlan => BuildGuidedBuildProposal(plan, rulesetId, archetype, optimization, legality, complexity, gmRequirements),
            AliceSupportMode.SettingsHandoff => BuildSettingsProposal(plan, rulesetId, archetype, optimization, legality, complexity, gmRequirements),
            _ => new AliceProposal(
                "No ALICE proposal is available from the current desktop surface.",
                "ALICE could not find a supported editable frame. Open a gear, combat, magic, contacts, skills, or build surface first.",
                "Nothing to apply.",
                "Open a runner surface first.",
                "No handoff is available.",
                null,
                null,
                "No action is available.")
        };
    }

    private static AliceProposal BuildQuickAddProposal(
        AliceSurfacePlan plan,
        string rulesetId,
        string archetype,
        string optimization,
        string legality,
        string complexity,
        string gmRequirements)
    {
        WorkspaceQuickAddRequest request = plan.SurfaceId switch
        {
            "gear" => BuildGearRequest(archetype, optimization),
            "weapons" => BuildWeaponRequest(archetype, optimization),
            "armors" => BuildArmorRequest(optimization),
            "cyberwares" => BuildCyberwareRequest(archetype, optimization),
            "drugs" => BuildDrugRequest(archetype),
            "spells" => BuildSpellRequest(archetype, optimization),
            "powers" => BuildPowerRequest(archetype, optimization),
            "complexforms" => BuildComplexFormRequest(optimization),
            "initiationgrades" => BuildInitiationRequest(optimization),
            "spirits" => BuildSpiritRequest(optimization),
            "critterpowers" => BuildCritterPowerRequest(),
            "aiprograms" => BuildMatrixProgramRequest(archetype),
            "vehicles" => BuildVehicleRequest(archetype),
            "contacts" => BuildContactRequest(archetype),
            "skills" => BuildSkillRequest(archetype, optimization),
            "qualities" => BuildQualityRequest(archetype),
            _ => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Gear, "Medkit Rating 6", Category: "Medical", Source: "Core Rulebook", Rating: 6)
        };

        string changeList =
            $"Add | {request.Name}{Environment.NewLine}" +
            $"Kind | {request.Kind}{Environment.NewLine}" +
            $"Bias | {optimization}{Environment.NewLine}" +
            $"Legality | {legality}{Environment.NewLine}" +
            $"Complexity | {complexity}{FormatGmRequirementLine(gmRequirements)}";
        string warningList = legality switch
        {
            "anything" => "Review availability and legality before applying. Alice is not overriding table policy.",
            "standard" => "Review table-specific legality and campaign overlays before applying.",
            _ => "Strict legality posture selected. Alice stayed on safer defaults where possible."
        };
        warningList = AppendGmRequirementWarning(warningList, gmRequirements);
        if (IsPreviewRuleset(rulesetId))
        {
            warningList += $"{Environment.NewLine}SR4 preview: review BP/Karma legality, source coverage, and table posture before committing.";
        }

        string summary = IsPreviewRuleset(rulesetId)
            ? $"Alice suggests adding {request.Name} on the {plan.SurfaceLabel.ToLowerInvariant()} surface for an SR4 {FormatChoiceLabel(archetype)} preview with a {optimization} bias."
            : $"Alice suggests adding {request.Name} on the {plan.SurfaceLabel.ToLowerInvariant()} surface for a {FormatChoiceLabel(archetype)} with a {optimization} bias.";
        string applyNotice = IsPreviewRuleset(rulesetId)
            ? $"Alice added SR4 preview item '{request.Name}' to {plan.SurfaceLabel.ToLowerInvariant()}."
            : $"Alice added '{request.Name}' to {plan.SurfaceLabel.ToLowerInvariant()}.";

        return new AliceProposal(
            $"Review the {plan.SurfaceLabel.ToLowerInvariant()} proposal, then apply it explicitly.",
            summary,
            changeList,
            warningList,
            string.Empty,
            null,
            request,
            applyNotice);
    }

    private static AliceProposal BuildGuidedBuildProposal(
        AliceSurfacePlan plan,
        string rulesetId,
        string archetype,
        string optimization,
        string legality,
        string complexity,
        string gmRequirements)
    {
        bool sr4Preview = IsPreviewRuleset(rulesetId);
        string changeList =
            $"Archetype | {FormatChoiceLabel(archetype)}{Environment.NewLine}" +
            $"Bias | {optimization}{Environment.NewLine}" +
            $"Legality | {legality}{Environment.NewLine}" +
            $"Complexity | {complexity}{Environment.NewLine}" +
            $"Scope | Complete first-pass character from scratch{FormatGmRequirementLine(gmRequirements)}{Environment.NewLine}" +
            $"Next step | Open the guided {(sr4Preview ? "SR4 BP/Karma" : "new-character")} workflow";
        string warningList = sr4Preview
            ? "SR4 remains a promoted preview. ALICE is shaping the build plan, not mutating the build silently, and the explicit BP or Karma workflow still owns the final build."
            : "This surface still needs the named character-creation workflow. ALICE is shaping the build plan, not mutating the build silently.";
        warningList = AppendGmRequirementWarning(warningList, gmRequirements);
        string summary = sr4Preview
            ? $"ALICE recommends an SR4 {FormatChoiceLabel(archetype)} preview start with {optimization} posture. Use the guided BP or Karma workflow next."
            : $"ALICE recommends a {FormatChoiceLabel(archetype)} start with {optimization} posture. Use the guided character workflow next.";

        return new AliceProposal(
            sr4Preview
                ? "ALICE shaped the SR4 preview build. Open the BP or Karma workflow to continue."
                : "ALICE shaped the build plan. Open the character workflow to continue.",
            summary,
            changeList,
            warningList,
            "Open new character workflow",
            plan.HandoffCommandId,
            null,
            string.Empty);
    }

    private static AliceProposal BuildSettingsProposal(
        AliceSurfacePlan plan,
        string rulesetId,
        string archetype,
        string optimization,
        string legality,
        string complexity,
        string gmRequirements)
    {
        string changeList =
            $"Compact posture | {(string.Equals(complexity, "simple", StringComparison.Ordinal) ? "On" : "Off")}{Environment.NewLine}" +
            $"Bias | {optimization}{Environment.NewLine}" +
            $"Legality posture | {legality}{Environment.NewLine}" +
            $"Archetype anchor | {FormatChoiceLabel(archetype)}{FormatGmRequirementLine(gmRequirements)}";
        string warningList = IsPreviewRuleset(rulesetId)
            ? "SR4 remains a preview lane. Settings changes still require the named settings form, and Alice is only suggesting safer defaults here."
            : "Settings changes still require the named settings lane. Alice is only suggesting defaults here.";
        warningList = AppendGmRequirementWarning(warningList, gmRequirements);
        string summary = IsPreviewRuleset(rulesetId)
            ? $"Alice suggests calmer SR4 preview defaults for a {FormatChoiceLabel(archetype)} workflow, then hands you back to settings for the actual edit."
            : $"Alice suggests calmer defaults for a {FormatChoiceLabel(archetype)} workflow, then hands you back to settings for the actual edit.";

        return new AliceProposal(
            "Review the suggested defaults, then open the settings lane.",
            summary,
            changeList,
            warningList,
            string.Equals(plan.HandoffCommandId, "global_settings", StringComparison.Ordinal) ? "Open global settings" : "Open character settings",
            plan.HandoffCommandId,
            null,
            string.Empty);
    }

    private static AliceProposal BuildRulesCoachProposal(
        string rulesetId,
        string archetype,
        string optimization,
        string legality,
        string complexity,
        string gmRequirements)
    {
        string changeList =
            $"Strict | Avoid restricted picks and table exceptions until the GM approves them.{Environment.NewLine}" +
            $"Standard | Allow common legal restricted choices, then flag what needs review.{Environment.NewLine}" +
            $"Anything | Include table exceptions, but never treat them as legal without GM approval.{Environment.NewLine}" +
            $"Ware | Explain essence, nuyen, availability, legality, and recovery tradeoffs before suggesting it.{Environment.NewLine}" +
            $"Qualities | Separate story hooks from mechanical picks; negative qualities must still fit the rules and table.{Environment.NewLine}" +
            $"Current frame | {RulesetLabel(rulesetId)} {FormatChoiceLabel(archetype)} · {optimization} · {legality} · {complexity}{FormatGmRequirementLine(gmRequirements)}";
        string warningList = AppendGmRequirementWarning(
            "Rules coach explains tradeoffs and sequencing. It does not override edition rules, source coverage, or GM constraints.",
            gmRequirements);

        return new AliceProposal(
            "Rules coach explanation ready.",
            $"ALICE can explain {RulesetLabel(rulesetId)} build settings, legality posture, ware tradeoffs, qualities, and availability before you commit a pick.",
            changeList,
            warningList,
            string.Empty,
            null,
            null,
            string.Empty);
    }

    private static AliceProposal BuildOriginDossierProposal(
        string rulesetId,
        string archetype,
        string optimization,
        string legality,
        string complexity,
        string gmRequirements)
    {
        string changeList =
            $"Dossier | Origin story, build seed, GM hooks, portrait prompts, scene prompts, PDF, audiobook notes, and video plan{Environment.NewLine}" +
            $"Ruleset | {RulesetLabel(rulesetId)}{Environment.NewLine}" +
            $"Archetype | {FormatChoiceLabel(archetype)}{Environment.NewLine}" +
            $"Tone | {optimization} · {legality} · {complexity}{FormatGmRequirementLine(gmRequirements)}{Environment.NewLine}" +
            "Sheet Changes | none; finished characters are not rewritten";
        string warningList = AppendGmRequirementWarning(
            "Origin dossier creates story and media. ALICE may use the story as a seed for suggestions, but mechanics still require explicit character-creation or edit actions.",
            gmRequirements);

        return new AliceProposal(
            "Origin dossier is ready to start.",
            $"ALICE can create an origin dossier for a {RulesetLabel(rulesetId)} {FormatChoiceLabel(archetype)} before creation or for a finished character.",
            changeList,
            warningList,
            "Open Origin Dossier",
            "new_character_origin",
            null,
            string.Empty);
    }

    private static string NormalizeConversationMode(string? value)
        => value switch
        {
            RulesCoachConversationMode => RulesCoachConversationMode,
            OriginDossierConversationMode => OriginDossierConversationMode,
            _ => BuildHelpConversationMode
        };

    private static string FormatGmRequirementLine(string gmRequirements)
    {
        if (string.IsNullOrWhiteSpace(gmRequirements))
        {
            return string.Empty;
        }

        string interpretation = FormatGmRequirementInterpretation(gmRequirements);
        return string.IsNullOrWhiteSpace(interpretation)
            ? $"{Environment.NewLine}GM Requirements | {gmRequirements.Trim()}"
            : $"{Environment.NewLine}GM Requirements | {gmRequirements.Trim()}{Environment.NewLine}{interpretation}";
    }

    private static string AppendGmRequirementWarning(string warningList, string gmRequirements)
    {
        if (string.IsNullOrWhiteSpace(gmRequirements))
        {
            return warningList;
        }

        string interpretation = FormatGmRequirementInterpretation(gmRequirements);
        string suffix = string.IsNullOrWhiteSpace(interpretation)
            ? gmRequirements.Trim()
            : $"{gmRequirements.Trim()}{Environment.NewLine}{interpretation}";
        return $"{warningList}{Environment.NewLine}GM requirements are treated as constraints or grants to explain, not silent sheet edits: {suffix}";
    }

    private static string FormatGmRequirementInterpretation(string gmRequirements)
    {
        string normalized = gmRequirements.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        List<string> interpretations = [];
        if (ContainsAny(normalized, "addict", "illegal drug", "drug addiction"))
        {
            interpretations.Add("GM Interpretation | Drug addiction constraint: use as a story hook and quality candidate, not an automatic sheet edit.");
        }

        if (ContainsAny(normalized, "magically active", "magic active", "awakened"))
        {
            interpretations.Add("GM Interpretation | Magical activity constraint: build seed must choose a compatible awakened path.");
        }

        if (ContainsAny(normalized, "intelligence", "logic", "intuition", "attribute floor", "2+"))
        {
            interpretations.Add("GM Interpretation | Attribute floor: preserve the named minimum before optimizing other picks.");
        }

        if (ContainsAny(normalized, "nuyen", "¥", "money", "cash", "budget", "+"))
        {
            interpretations.Add("GM Interpretation | Money grant: track bonus funds separately from normal creation resources.");
        }

        if (ContainsAny(normalized, "ware", "availability", "restricted", "forbidden", "illegal"))
        {
            interpretations.Add("GM Interpretation | Ware or availability exception: explain the exception and require review before any mechanical edit.");
        }

        if (ContainsAny(normalized, "quality", "qualities"))
        {
            interpretations.Add("GM Interpretation | Quality grant: propose candidate qualities, then require explicit selection.");
        }

        if (ContainsAny(normalized, "gear", "item", "equipment"))
        {
            interpretations.Add("GM Interpretation | Gear grant: list the item as a grant candidate before adding it to the sheet.");
        }

        return string.Join(Environment.NewLine, interpretations.Distinct(StringComparer.Ordinal));
    }

    private static bool ContainsAny(string value, params string[] needles)
        => needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static string RulesetLabel(string rulesetId)
        => string.Equals(rulesetId, RulesetDefaults.Sr4, StringComparison.Ordinal)
            ? "SR4"
            : string.Equals(rulesetId, RulesetDefaults.Sr6, StringComparison.Ordinal)
                ? "SR6"
                : "SR5";

    private static WorkspaceQuickAddRequest BuildGearRequest(string archetype, string optimization)
    {
        int rating = ResolveScale(optimization, low: 3, medium: 4, high: 6);
        return archetype switch
        {
            "decker" => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Gear, "Tag Eraser", Category: "Electronics", Source: "Core Rulebook"),
            "mage" => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Gear, "Reagents", Category: "Magic", Source: "Core Rulebook", Quantity: ResolveScale(optimization, 5, 10, 20)),
            "face" => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Gear, "Fake SIN Rating 4", Category: "Identification", Source: "Core Rulebook", Rating: rating),
            "rigger" => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Gear, "Tool Kit", Category: "Mechanic", Source: "Core Rulebook"),
            _ => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Gear, "Medkit Rating 6", Category: "Medical", Source: "Core Rulebook", Rating: rating)
        };
    }

    private static WorkspaceQuickAddRequest BuildWeaponRequest(string archetype, string optimization)
        => archetype switch
        {
            "rigger" => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Weapon, "Defiance T-250", Category: "Shotguns", Source: "Core Rulebook", Accuracy: "4", Damage: "13P", Ap: "-1", Mode: "SS/SA"),
            "street_sam" or "adept" => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Weapon, "Colt M23", Category: "Heavy Pistols", Source: "Core Rulebook", Accuracy: "5", Damage: $"{ResolveScale(optimization, 7, 8, 9)}P", Ap: "-1", Mode: "SA"),
            _ => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Weapon, "Ares Predator V", Category: "Heavy Pistols", Source: "Core Rulebook", Accuracy: "5", Damage: "8P", Ap: "-1", Mode: "SA")
        };

    private static bool IsPreviewRuleset(string rulesetId)
        => string.Equals(rulesetId, RulesetDefaults.Sr4, StringComparison.Ordinal);

    private static WorkspaceQuickAddRequest BuildArmorRequest(string optimization)
        => new(WorkspaceQuickAddKinds.Armor, "Armor Jacket", Category: "Armor", Source: "Core Rulebook", ArmorValue: ResolveScale(optimization, 10, 12, 13).ToString());

    private static WorkspaceQuickAddRequest BuildCyberwareRequest(string archetype, string optimization)
        => archetype switch
        {
            "street_sam" => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Cyberware, "Wired Reflexes 2", Category: "Bodyware", Source: "Core Rulebook", Rating: ResolveScale(optimization, 1, 2, 2), Essence: "3.00"),
            "decker" => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Cyberware, "Datajack", Category: "Headware", Source: "Core Rulebook", Essence: "0.10"),
            "face" => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Cyberware, "Tailored Pheromones 2", Category: "Bioware", Source: "Core Rulebook", Rating: 2, Essence: "0.40"),
            "rigger" => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Cyberware, "Control Rig 2", Category: "Headware", Source: "Core Rulebook", Rating: 2, Essence: "0.50"),
            _ => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Cyberware, "Cybereyes Rating 4", Category: "Headware", Source: "Core Rulebook", Rating: ResolveScale(optimization, 2, 4, 4), Essence: "0.40")
        };

    private static WorkspaceQuickAddRequest BuildDrugRequest(string archetype)
        => archetype switch
        {
            "decker" => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Drug, "Psyche", Category: "Drugs", Source: "Core Rulebook"),
            _ => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Drug, "Jazz", Category: "Drugs", Source: "Core Rulebook")
        };

    private static WorkspaceQuickAddRequest BuildSpellRequest(string archetype, string optimization)
        => archetype switch
        {
            "mage" => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Spell, "Stunbolt", Category: "Combat", Source: "Core Rulebook", Type: "M", Range: "LOS", Duration: "I", DrainValue: ResolveScale(optimization, 2, 3, 4).ToString()),
            _ => new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Spell, "Heal", Category: "Health", Source: "Core Rulebook", Type: "M", Range: "T", Duration: "P", DrainValue: "3")
        };

    private static WorkspaceQuickAddRequest BuildPowerRequest(string archetype, string optimization)
        => new(WorkspaceQuickAddKinds.Power, string.Equals(archetype, "adept", StringComparison.Ordinal) ? "Improved Reflexes" : "Combat Sense", Category: "Adept Power", Source: "Core Rulebook", Rating: ResolveScale(optimization, 1, 2, 3), PointsPerLevel: 1m);

    private static WorkspaceQuickAddRequest BuildComplexFormRequest(string optimization)
        => new(WorkspaceQuickAddKinds.ComplexForm, "Cleaner", Category: "Complex Form", Source: "Core Rulebook", Rating: ResolveScale(optimization, 2, 3, 6), Target: "File", Duration: "S", FadingValue: "1");

    private static WorkspaceQuickAddRequest BuildInitiationRequest(string optimization)
        => new(WorkspaceQuickAddKinds.InitiationGrade, "Initiation Grade", Source: "Core Rulebook", Rating: ResolveScale(optimization, 1, 1, 2), Karma: ResolveScale(optimization, 10, 13, 16), Reward: "Initiation");

    private static WorkspaceQuickAddRequest BuildSpiritRequest(string optimization)
        => new(WorkspaceQuickAddKinds.Spirit, "Air Spirit", Category: "Spirit", Source: "Core Rulebook", Force: ResolveScale(optimization, 3, 4, 6), Services: ResolveScale(optimization, 1, 2, 3));

    private static WorkspaceQuickAddRequest BuildCritterPowerRequest()
        => new(WorkspaceQuickAddKinds.CritterPower, "Enhanced Senses", Category: "Critter Power", Source: "Core Rulebook");

    private static WorkspaceQuickAddRequest BuildMatrixProgramRequest(string archetype)
        => new(WorkspaceQuickAddKinds.MatrixProgram, string.Equals(archetype, "decker", StringComparison.Ordinal) ? "Browse" : "Armor", Category: "Program", Source: "Core Rulebook", Slot: "Program Slot");

    private static WorkspaceQuickAddRequest BuildVehicleRequest(string archetype)
        => new(WorkspaceQuickAddKinds.Vehicle, string.Equals(archetype, "rigger", StringComparison.Ordinal) ? "GMC Roadmaster" : "Yamaha Growler", Category: "Vehicle", Source: "Core Rulebook", Handling: "4", Speed: "3", Body: "16", Sensor: "2", Seats: "2");

    private static WorkspaceQuickAddRequest BuildContactRequest(string archetype)
    {
        (string Name, string Role, int Connection, int Loyalty) = archetype switch
        {
            "decker" => ("Grid Broker", "Matrix Broker", 3, 2),
            "mage" => ("Talismonger", "Talismonger", 3, 2),
            "face" => ("Mr. Johnson", "Johnson", 4, 2),
            "rigger" => ("Mechanic", "Mechanic", 3, 2),
            _ => ("Fixer", "Fixer", 4, 3)
        };

        return new WorkspaceQuickAddRequest(
            WorkspaceQuickAddKinds.Contact,
            Name,
            Role: Role,
            Connection: Connection,
            Loyalty: Loyalty);
    }

    private static WorkspaceQuickAddRequest BuildSkillRequest(string archetype, string optimization)
    {
        string name = archetype switch
        {
            "decker" => "Hacking",
            "mage" => "Spellcasting",
            "face" => "Con",
            "rigger" => "Pilot Ground Craft",
            "adept" => "Gymnastics",
            _ => "Perception"
        };

        return new WorkspaceQuickAddRequest(
            WorkspaceQuickAddKinds.Skill,
            name,
            Category: "Active Skill",
            Rating: ResolveScale(optimization, 2, 3, 5));
    }

    private static WorkspaceQuickAddRequest BuildQualityRequest(string archetype)
    {
        string name = archetype switch
        {
            "decker" => "Codeslinger",
            "mage" => "Focused Concentration",
            "face" => "First Impression",
            "rigger" => "Gearhead",
            _ => "Toughness"
        };

        return new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Quality, name, Category: "Quality", Source: "Core Rulebook", Karma: 10);
    }

    private static int ResolveScale(string optimization, int low, int medium, int high)
        => optimization switch
        {
            "cheap" => low,
            "survivable" => medium,
            "specialized" => high,
            _ => medium
        };

    private static string FormatChoiceLabel(string value)
        => value.Replace('_', ' ');

    private sealed record AliceSurfacePlan(
        string SurfaceId,
        string SurfaceLabel,
        AliceSupportMode SupportMode,
        string SupportLabel,
        string SurfaceSummary,
        string? HandoffCommandId);

    private sealed record AliceProposal(
        string Message,
        string Summary,
        string ChangeList,
        string WarningList,
        string HandoffLabel,
        string? HandoffCommandId,
        WorkspaceQuickAddRequest? Request,
        string ApplyNotice);

    private enum AliceSupportMode
    {
        Unavailable,
        QuickAddApply,
        GuidedBuildPlan,
        SettingsHandoff
    }
}
