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
    private const string RulesetFieldId = "autoAliceRulesetId";
    private const string WorkspaceFieldId = "autoAliceWorkspaceId";
    private const string ArchetypeFieldId = "autoAliceArchetype";
    private const string OptimizationFieldId = "autoAliceOptimization";
    private const string LegalityFieldId = "autoAliceLegality";
    private const string ComplexityFieldId = "autoAliceComplexity";

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
        string message = plan.SupportMode switch
        {
            AliceSupportMode.QuickAddApply => $"ALICE can propose one scoped change for {plan.SurfaceLabel.ToLowerInvariant()}. Answer the questions, preview the result, then apply it explicitly.",
            AliceSupportMode.GuidedBuildPlan => "ALICE can shape the current build lane, but this frame still needs a guided workflow handoff instead of silent mutation.",
            AliceSupportMode.SettingsHandoff => "ALICE can suggest a sane settings posture here, then hand you to the real settings form for deliberate edits.",
            _ => "No editable runner surface is active. Open a build, gear, combat, magic, contacts, or settings lane first."
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
        string? stored = DesktopDialogFieldValueParser.GetValue(dialog, HandoffCommandFieldId);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        AliceSurfacePlan plan = ResolvePlanFromDialog(dialog, state.ActiveSectionId, state.ActiveDialog?.Id);
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
                "Interview",
                "Answer a few questions first. ALICE stays scoped to the current visible frame.",
                "Answer a few questions first.",
                IsReadOnly: true,
                IsMultiline: true,
                VisualKind: DesktopDialogFieldVisualKinds.Snippet),
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
                ])
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
            _ => BuildPlan("unavailable", "Current Surface", AliceSupportMode.Unavailable)
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
            _ => "Unavailable"
        };

    private static string ResolveSurfaceSummary(string surfaceId, AliceSupportMode mode)
        => mode switch
        {
            AliceSupportMode.QuickAddApply => $"ALICE can add one reasonable {surfaceId.Replace('_', ' ')} proposal on first-party rails.",
            AliceSupportMode.GuidedBuildPlan => "ALICE can recommend a build direction, but the create surface still needs the named workflow.",
            AliceSupportMode.SettingsHandoff => "ALICE can suggest defaults, but the settings lane still owns the actual edits.",
            _ => "No editable ALICE surface is currently active."
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

        return plan.SupportMode switch
        {
            AliceSupportMode.QuickAddApply => BuildQuickAddProposal(plan, archetype, optimization, legality, complexity),
            AliceSupportMode.GuidedBuildPlan => BuildGuidedBuildProposal(plan, archetype, optimization, legality, complexity),
            AliceSupportMode.SettingsHandoff => BuildSettingsProposal(plan, archetype, optimization, legality, complexity),
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
        string archetype,
        string optimization,
        string legality,
        string complexity)
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
            $"Complexity | {complexity}";
        string warningList = legality switch
        {
            "anything" => "Review availability and legality before applying. ALICE is not overriding table policy.",
            "standard" => "Review table-specific legality and campaign overlays before applying.",
            _ => "Strict legality posture selected. ALICE stayed on safer defaults where possible."
        };
        string summary = $"ALICE suggests adding {request.Name} on the {plan.SurfaceLabel.ToLowerInvariant()} surface for a {FormatChoiceLabel(archetype)} with a {optimization} bias.";
        string applyNotice = $"ALICE added '{request.Name}' to {plan.SurfaceLabel.ToLowerInvariant()}.";

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
        string archetype,
        string optimization,
        string legality,
        string complexity)
    {
        string changeList =
            $"Archetype | {FormatChoiceLabel(archetype)}{Environment.NewLine}" +
            $"Bias | {optimization}{Environment.NewLine}" +
            $"Legality | {legality}{Environment.NewLine}" +
            $"Complexity | {complexity}{Environment.NewLine}" +
            "Next step | Open the guided new-character workflow";
        string warningList = "This surface still needs the named character-creation workflow. ALICE is shaping the lane, not mutating the build silently.";
        string summary = $"ALICE recommends a {FormatChoiceLabel(archetype)} start with {optimization} posture. Use the guided character workflow next.";

        return new AliceProposal(
            "ALICE shaped the build lane. Open the character workflow to continue.",
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
        string archetype,
        string optimization,
        string legality,
        string complexity)
    {
        string changeList =
            $"Compact posture | {(string.Equals(complexity, "simple", StringComparison.Ordinal) ? "On" : "Off")}{Environment.NewLine}" +
            $"Bias | {optimization}{Environment.NewLine}" +
            $"Legality posture | {legality}{Environment.NewLine}" +
            $"Archetype anchor | {FormatChoiceLabel(archetype)}";
        string warningList = "Settings changes still require the named settings lane. ALICE is only suggesting defaults here.";
        string summary = $"ALICE suggests calmer defaults for a {FormatChoiceLabel(archetype)} workflow, then hands you back to settings for the actual edit.";

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
