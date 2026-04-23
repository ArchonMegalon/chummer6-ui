using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Rulesets;
using Chummer.Presentation.Shell;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Chummer.Presentation.Overview;

public sealed class DialogCoordinator : IDialogCoordinator
{
    private readonly IEngineEvaluator _engineEvaluator;

    public DialogCoordinator(IEngineEvaluator? engineEvaluator = null)
    {
        _engineEvaluator = engineEvaluator ?? new NullEngineEvaluator();
    }

    public async Task CoordinateAsync(string actionId, DialogCoordinationContext context, CancellationToken ct)
    {
        DesktopDialogState? dialog = context.State.ActiveDialog;
        if (dialog is null)
            return;

        if (string.IsNullOrWhiteSpace(actionId))
        {
            context.Publish(context.State with { Error = "Dialog action id is required." });
            return;
        }

        switch (actionId)
        {
            case "cancel":
            case "close":
                context.Publish(context.State with
                {
                    ActiveDialog = null,
                    Error = null
                });
                return;
            default:
                break;
        }

        if (string.Equals(dialog.Id, "dialog.workspace.metadata", StringComparison.Ordinal) && string.Equals(actionId, "apply_metadata", StringComparison.Ordinal))
        {
            await ApplyMetadataDialogAsync(dialog, context, ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.open_character", StringComparison.Ordinal)
            && string.Equals(actionId, "import", StringComparison.Ordinal))
        {
            await ImportCharacterDialogAsync(dialog, context, ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.open_for_printing", StringComparison.Ordinal)
            && string.Equals(actionId, "import", StringComparison.Ordinal))
        {
            await ImportCharacterDialogAsync(
                dialog,
                context,
                ct,
                successNotice: "Character imported for printing.",
                afterImportAsync: context.PrintAsync);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.open_for_export", StringComparison.Ordinal)
            && string.Equals(actionId, "import", StringComparison.Ordinal))
        {
            await ImportCharacterDialogAsync(
                dialog,
                context,
                ct,
                successNotice: "Character imported for export.",
                afterImportAsync: context.ExportAsync);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.new_character", StringComparison.Ordinal)
            && string.Equals(actionId, "create_character", StringComparison.Ordinal))
        {
            await CreateCharacterFromDialogAsync(dialog, context, ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.hero_lab_importer", StringComparison.Ordinal)
            && string.Equals(actionId, "import", StringComparison.Ordinal))
        {
            await ImportCharacterDialogAsync(
                dialog,
                context,
                ct,
                fieldId: "heroLabXml",
                requiredError: "Hero Lab XML is required.",
                successNotice: "Hero Lab XML imported.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.dice_roller", StringComparison.Ordinal) && string.Equals(actionId, "roll", StringComparison.Ordinal))
        {
            await RollDiceAsync(dialog, context, ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.dice_roller", StringComparison.Ordinal) && string.Equals(actionId, "reroll_misses", StringComparison.Ordinal))
        {
            RerollMisses(dialog, context);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.global_settings", StringComparison.Ordinal)
            && (string.Equals(actionId, "save", StringComparison.Ordinal) || string.Equals(actionId, "apply", StringComparison.Ordinal)))
        {
            ApplyGlobalSettings(dialog, context, closeDialog: string.Equals(actionId, "save", StringComparison.Ordinal));
            return;
        }

        if (string.Equals(dialog.Id, "dialog.switch_ruleset", StringComparison.Ordinal) && string.Equals(actionId, "apply_ruleset", StringComparison.Ordinal))
        {
            await ApplyPreferredRulesetAsync(dialog, context, ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.character_settings", StringComparison.Ordinal) && string.Equals(actionId, "save", StringComparison.Ordinal))
        {
            ApplyCharacterSettings(dialog, context);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.character_roster", StringComparison.Ordinal))
        {
            switch (actionId)
            {
                case "refresh_watch_folder":
                    RefreshCharacterRosterDialog(dialog, context);
                    return;
                case "open_runner":
                    OpenCharacterRosterRunner(dialog, context);
                    return;
                case "open_watch_file":
                    OpenCharacterRosterWatchFile(dialog, context);
                    return;
                case "open_roster_folder":
                    OpenCharacterRosterFolder(dialog, context);
                    return;
                case "open_portrait":
                    PublishCharacterRosterCommandNotice(dialog, context, "rosterPortraitPath", "No portrait slot is currently matched.", portraitPath => $"Portrait slot '{Path.GetFileName(portraitPath)}' surfaced in the roster workbench.");
                    return;
            }
        }

        if (string.Equals(dialog.Id, "dialog.master_index", StringComparison.Ordinal))
        {
            switch (actionId)
            {
                case "open_source":
                    PublishMasterIndexDialog(
                        context,
                        dialog,
                        $"Linked source for '{ReadDialogValue(dialog, "masterIndexCurrentSourcebook", "current sourcebook")}' remains pinned on the right.");
                    return;
                case "switch_file":
                    CycleMasterIndexFile(dialog, context);
                    return;
                case "switch_sourcebook":
                    CycleMasterIndexSourcebook(dialog, context);
                    return;
            }
        }

        if (TryCoordinateLegacySelectionAction(dialog, actionId, context))
        {
            return;
        }

        if (TryCoordinateLegacyDeleteAction(dialog, actionId, context))
        {
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.open_notes", StringComparison.Ordinal) && string.Equals(actionId, "save", StringComparison.Ordinal))
        {
            string notes = DesktopDialogFieldValueParser.GetValue(dialog, "uiNotesEditor") ?? string.Empty;
            await context.UpdateMetadataAsync(new UpdateWorkspaceMetadata(
                context.State.Profile?.Name,
                context.State.Profile?.Alias,
                notes), ct);
            CharacterOverviewState stateAfterUpdate = context.GetState();
            if (!string.IsNullOrWhiteSpace(stateAfterUpdate.Error))
            {
                return;
            }

            context.Publish(stateAfterUpdate with
            {
                ActiveDialog = null,
                Error = null,
                Preferences = stateAfterUpdate.Preferences with
                {
                    CharacterNotes = notes
                },
                Notice = "Notes saved."
            });
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.create_entry", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string entryName = ReadDialogValue(dialog, "uiCreateEntryName", "New entry");
            PublishRulesetAwareDialogNotice(context, $"Entry '{entryName}' added.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.create_entry", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            PublishRulesetAwareDialogAddMore(context, dialog, "Entry added. Editor remains open for another entry.", "uiCreateEntryName");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.edit_entry", StringComparison.Ordinal) && string.Equals(actionId, "apply", StringComparison.Ordinal))
        {
            string entryName = ReadDialogValue(dialog, "uiEditEntryName", "Current Entry");
            PublishRulesetAwareDialogNotice(context, $"Entry renamed to '{entryName}'.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.gear_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string gearName = ReadDialogValue(dialog, "uiGearName", "Ares Predator");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildGearQuickAddRequest(dialog),
                $"Gear '{gearName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.gear_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string gearName = ReadDialogValue(dialog, "uiGearName", "Ares Predator");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildGearQuickAddRequest(dialog),
                $"Gear '{gearName}' added. Dialog remains open for another item.",
                ct,
                "uiGearQuantity",
                "uiGearMarkup",
                "uiGearFreeItem");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.gear_edit", StringComparison.Ordinal) && string.Equals(actionId, "apply", StringComparison.Ordinal))
        {
            string gearName = ReadDialogValue(dialog, "uiGearEditName", "Selected Gear");
            PublishRulesetAwareDialogNotice(context, $"Gear renamed to '{gearName}'.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.drug_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string drugName = ReadDialogValue(dialog, "uiDrugName", "Jazz");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildDrugQuickAddRequest(dialog),
                $"Drug '{drugName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.drug_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string drugName = ReadDialogValue(dialog, "uiDrugName", "Jazz");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildDrugQuickAddRequest(dialog),
                $"Drug '{drugName}' added. Dialog remains open for another dose.",
                ct,
                "uiDrugQuantity");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.cyberware_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string cyberwareName = ReadDialogValue(dialog, "uiCyberwareName", "Wired Reflexes 2");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildCyberwareQuickAddRequest(dialog),
                $"Cyberware '{cyberwareName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.cyberware_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string cyberwareName = ReadDialogValue(dialog, "uiCyberwareName", "Wired Reflexes 2");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildCyberwareQuickAddRequest(dialog),
                $"Cyberware '{cyberwareName}' added. Dialog remains open for another implant.",
                ct,
                "uiCyberwareRating",
                "uiCyberwareMarkup",
                "uiCyberwareDiscount");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.magic_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string magicName = ReadDialogValue(dialog, "uiMagicName", "Spell or Power");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildMagicQuickAddRequest(dialog),
                $"Spell/power '{magicName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.magic_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string magicName = ReadDialogValue(dialog, "uiMagicName", "Spell or Power");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildMagicQuickAddRequest(dialog),
                $"Spell/power '{magicName}' added. Dialog remains open for another selection.",
                ct,
                "uiMagicLevel");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.spell_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string spellName = ReadDialogValue(dialog, "uiSpellName", "Stunbolt");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildSpellQuickAddRequest(dialog),
                $"Spell '{spellName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.spell_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string spellName = ReadDialogValue(dialog, "uiSpellName", "Stunbolt");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildSpellQuickAddRequest(dialog),
                $"Spell '{spellName}' added. Dialog remains open for another spell.",
                ct,
                "uiSpellExtendedOnly");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.adept_power_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string powerName = ReadDialogValue(dialog, "uiAdeptPowerName", "Improved Reflexes");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildAdeptPowerQuickAddRequest(dialog),
                $"Adept power '{powerName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.adept_power_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string powerName = ReadDialogValue(dialog, "uiAdeptPowerName", "Improved Reflexes");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildAdeptPowerQuickAddRequest(dialog),
                $"Adept power '{powerName}' added. Dialog remains open for another power.",
                ct,
                "uiAdeptPowerLevel");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.complex_form_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string formName = ReadDialogValue(dialog, "uiComplexFormName", "Cleaner");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildComplexFormQuickAddRequest(dialog),
                $"Complex form '{formName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.complex_form_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string formName = ReadDialogValue(dialog, "uiComplexFormName", "Cleaner");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildComplexFormQuickAddRequest(dialog),
                $"Complex form '{formName}' added. Dialog remains open for another form.",
                ct,
                "uiComplexFormLevel");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.skill_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string skillName = ReadDialogValue(dialog, "uiSkillName", "Perception");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildSkillQuickAddRequest(dialog),
                $"Skill '{skillName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.skill_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string skillName = ReadDialogValue(dialog, "uiSkillName", "Perception");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildSkillQuickAddRequest(dialog),
                $"Skill '{skillName}' added. Dialog remains open for another skill.",
                ct,
                "uiSkillRating");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.skill_specialize", StringComparison.Ordinal) && string.Equals(actionId, "apply", StringComparison.Ordinal))
        {
            string specialization = ReadDialogValue(dialog, "uiSkillSpec", "Visual");
            PublishRulesetAwareDialogNotice(context, $"Skill specialization set to '{specialization}'.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.combat_add_weapon", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string weaponName = ReadDialogValue(dialog, "uiWeaponName", "Colt M23");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildWeaponQuickAddRequest(dialog),
                $"Weapon '{weaponName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.combat_add_weapon", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string weaponName = ReadDialogValue(dialog, "uiWeaponName", "Colt M23");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildWeaponQuickAddRequest(dialog),
                $"Weapon '{weaponName}' added. Dialog remains open for another weapon.",
                ct,
                "uiWeaponMarkup",
                "uiWeaponFreeItem",
                "uiWeaponBlackMarketDiscount");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.combat_add_armor", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string armorName = ReadDialogValue(dialog, "uiArmorName", "Armor Jacket");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildArmorQuickAddRequest(dialog),
                $"Armor '{armorName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.combat_add_armor", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string armorName = ReadDialogValue(dialog, "uiArmorName", "Armor Jacket");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildArmorQuickAddRequest(dialog),
                $"Armor '{armorName}' added. Dialog remains open for another armor item.",
                ct,
                "uiArmorMarkup",
                "uiArmorFreeItem");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.contact_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string contactName = ReadDialogValue(dialog, "uiContactName", "Contact Name");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildContactQuickAddRequest(dialog),
                $"Contact '{contactName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.contact_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string contactName = ReadDialogValue(dialog, "uiContactName", "Contact Name");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildContactQuickAddRequest(dialog),
                $"Contact '{contactName}' added. Dialog remains open for another contact.",
                ct,
                "uiContactName",
                "uiContactConnection",
                "uiContactLoyalty");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.matrix_program_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string programName = ReadDialogValue(dialog, "uiMatrixProgramName", "Armor");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildMatrixProgramQuickAddRequest(dialog),
                $"Program '{programName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.matrix_program_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string programName = ReadDialogValue(dialog, "uiMatrixProgramName", "Armor");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildMatrixProgramQuickAddRequest(dialog),
                $"Program '{programName}' added. Dialog remains open for another matrix entry.",
                ct,
                "uiMatrixProgramShowDongles");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.initiation_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string rewardName = ReadDialogValue(dialog, "uiInitiationReward", "Masking");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildInitiationQuickAddRequest(dialog),
                $"Initiation/submersion reward '{rewardName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.initiation_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string rewardName = ReadDialogValue(dialog, "uiInitiationReward", "Masking");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildInitiationQuickAddRequest(dialog),
                $"Initiation/submersion reward '{rewardName}' added. Dialog remains open for another advancement step.",
                ct,
                "uiInitiationGrade");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.spirit_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string spiritName = ReadDialogValue(dialog, "uiSpiritName", "Watcher Spirit");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildSpiritQuickAddRequest(dialog),
                $"Spirit '{spiritName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.spirit_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string spiritName = ReadDialogValue(dialog, "uiSpiritName", "Watcher Spirit");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildSpiritQuickAddRequest(dialog),
                $"Spirit '{spiritName}' added. Dialog remains open for another spirit.",
                ct,
                "uiSpiritForce");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.critter_power_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string powerName = ReadDialogValue(dialog, "uiCritterPowerName", "Natural Weapon");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildCritterPowerQuickAddRequest(dialog),
                $"Critter power '{powerName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.critter_power_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string powerName = ReadDialogValue(dialog, "uiCritterPowerName", "Natural Weapon");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildCritterPowerQuickAddRequest(dialog),
                $"Critter power '{powerName}' added. Dialog remains open for another power.",
                ct,
                "uiCritterPowerRating");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.vehicle_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string vehicleName = ReadDialogValue(dialog, "uiVehicleName", "Hyundai Shin-Hyung");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildVehicleQuickAddRequest(dialog),
                $"Vehicle '{vehicleName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.vehicle_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string vehicleName = ReadDialogValue(dialog, "uiVehicleName", "Hyundai Shin-Hyung");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildVehicleQuickAddRequest(dialog),
                $"Vehicle '{vehicleName}' added. Dialog remains open for another entry.",
                ct,
                "uiVehicleShowDrones");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.quality_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string qualityName = ReadDialogValue(dialog, "uiQualityName", "First Impression");
            await ApplyQuickAddDialogAsync(
                context,
                dialog,
                BuildQualityQuickAddRequest(dialog),
                $"Quality '{qualityName}' added.",
                ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.quality_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string qualityName = ReadDialogValue(dialog, "uiQualityName", "First Impression");
            await ApplyQuickAddDialogAddMoreAsync(
                context,
                dialog,
                BuildQualityQuickAddRequest(dialog),
                $"Quality '{qualityName}' added. Dialog remains open for another quality.",
                ct,
                "uiQualityMetagenicOnly");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.contact_edit", StringComparison.Ordinal) && string.Equals(actionId, "apply", StringComparison.Ordinal))
        {
            string contactName = ReadDialogValue(dialog, "uiContactEditName", "Selected Contact");
            PublishRulesetAwareDialogNotice(context, $"Contact renamed to '{contactName}'.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.contact_connection", StringComparison.Ordinal) && string.Equals(actionId, "apply", StringComparison.Ordinal))
        {
            string connection = DesktopDialogFieldValueParser.GetValue(dialog, "uiContactConnection") ?? "0";
            string loyalty = DesktopDialogFieldValueParser.GetValue(dialog, "uiContactLoyalty") ?? "0";
            PublishRulesetAwareDialogNotice(context, $"Contact connection/loyalty applied ({connection}/{loyalty}).");
            return;
        }

        if ((string.Equals(dialog.Id, "dialog.data_exporter", StringComparison.Ordinal)
            || string.Equals(dialog.Id, "dialog.export_character", StringComparison.Ordinal))
            && string.Equals(actionId, "download", StringComparison.Ordinal))
        {
            if (context.ExportAsync is null)
            {
                PublishDialogNotice(context, "Export bundle prepared for download.");
                return;
            }

            await context.ExportAsync(ct);
            return;
        }

        if (string.Equals(dialog.Id, "dialog.print_character", StringComparison.Ordinal)
            && string.Equals(actionId, "print", StringComparison.Ordinal))
        {
            if (context.PrintAsync is null)
            {
                PublishDialogNotice(context, "Print preview prepared.");
                return;
            }

            await context.PrintAsync(ct);
            return;
        }

        context.Publish(context.State with
        {
            ActiveDialog = null,
            Error = null,
            Notice = $"{dialog.Title}: action '{actionId}' executed."
        });
    }

    private static async Task ApplyPreferredRulesetAsync(
        DesktopDialogState dialog,
        DialogCoordinationContext context,
        CancellationToken ct)
    {
        string? rulesetId = ReadRequiredRuleset(dialog, context, "preferredRulesetId", "Preferred ruleset is required.");
        if (rulesetId is null)
            return;

        if (context.SetPreferredRulesetAsync is null)
        {
            PublishDialogNotice(context, $"Preferred ruleset set to '{rulesetId}'.");
            return;
        }

        await context.SetPreferredRulesetAsync(rulesetId, ct);

        CharacterOverviewState stateAfterUpdate = context.GetState();
        if (stateAfterUpdate.Error is null)
        {
            context.Publish(stateAfterUpdate with
            {
                ActiveDialog = null,
                Error = null,
                Notice = $"Preferred ruleset set to '{rulesetId}'."
            });
        }
    }

    private static async Task ImportCharacterDialogAsync(
        DesktopDialogState dialog,
        DialogCoordinationContext context,
        CancellationToken ct,
        string fieldId = "openCharacterXml",
        string rulesetFieldId = "importRulesetId",
        string requiredError = "Character XML is required.",
        string successNotice = "Character imported.",
        Func<CancellationToken, Task>? afterImportAsync = null)
    {
        string xml = DesktopDialogFieldValueParser.GetValue(dialog, fieldId) ?? string.Empty;
        string? rulesetId = ReadRequiredRuleset(dialog, context, rulesetFieldId, "Ruleset is required.");
        if (string.IsNullOrWhiteSpace(xml))
        {
            context.Publish(context.State with
            {
                Error = requiredError,
                Notice = null
            });
            return;
        }

        if (rulesetId is null)
            return;

        await context.ImportAsync(new WorkspaceImportDocument(xml, rulesetId, WorkspaceDocumentFormat.NativeXml), ct);

        CharacterOverviewState stateAfterImport = context.GetState();
        if (stateAfterImport.Error is not null)
        {
            return;
        }

        if (afterImportAsync is not null)
        {
            await afterImportAsync(ct);
            CharacterOverviewState stateAfterFollowUp = context.GetState();
            if (stateAfterFollowUp.Error is null && stateAfterFollowUp.ActiveDialog is not null)
            {
                context.Publish(stateAfterFollowUp with
                {
                    ActiveDialog = null,
                    Error = null
                });
            }

            return;
        }

        context.Publish(stateAfterImport with
        {
            ActiveDialog = null,
            Error = null,
            Notice = stateAfterImport.Notice ?? successNotice
        });
    }

    private static string? ReadRequiredRuleset(
        DesktopDialogState dialog,
        DialogCoordinationContext context,
        string fieldId,
        string requiredError)
    {
        string? rulesetId = RulesetDefaults.NormalizeOptional(DesktopDialogFieldValueParser.GetValue(dialog, fieldId));
        if (!string.IsNullOrWhiteSpace(rulesetId))
            return rulesetId;

        context.Publish(context.State with
        {
            Error = requiredError,
            Notice = null
        });
        return null;
    }

    private static void ApplyGlobalSettings(DesktopDialogState dialog, DialogCoordinationContext context, bool closeDialog)
    {
        DesktopPreferenceState nextPreferences = DesktopDialogFactory.ParseGlobalSettingsPreferences(dialog, context.State.Preferences);
        string language = DesktopLocalizationCatalog.NormalizeOrDefault(nextPreferences.Language);
        bool languageChanged = !string.Equals(
            DesktopLocalizationCatalog.NormalizeOrDefault(context.State.Preferences.Language),
            language,
            StringComparison.Ordinal);
        DesktopDialogState? nextDialog = closeDialog
            ? null
            : DesktopDialogFactory.BuildGlobalSettingsDialog(
                nextPreferences,
                language,
                DesktopDialogFactory.ReadGlobalSettingsActivePane(dialog));

        context.Publish(context.State with
        {
            ActiveDialog = nextDialog,
            Error = null,
            Preferences = nextPreferences,
            Notice = languageChanged
                ? DesktopLocalizationCatalog.GetRequiredFormattedString(
                    "desktop.dialog.global_settings.notice.updated_restart",
                    language,
                    DesktopLocalizationCatalog.GetDisplayLabel(language))
                : DesktopLocalizationCatalog.GetRequiredString(
                    "desktop.dialog.global_settings.notice.updated",
                    language)
        });
    }

    private static void ApplyCharacterSettings(DesktopDialogState dialog, DialogCoordinationContext context)
    {
        string priority = DesktopDialogFieldValueParser.GetValue(dialog, "characterPriority") ?? context.State.Preferences.CharacterPriority;
        int karmaNuyenRatio = DesktopDialogFieldValueParser.ParseInt(dialog, "characterKarmaNuyen", context.State.Preferences.KarmaNuyenRatio);
        bool houseRules = DesktopDialogFieldValueParser.ParseBool(dialog, "characterHouseRulesEnabled", context.State.Preferences.HouseRulesEnabled);

        context.Publish(context.State with
        {
            ActiveDialog = null,
            Error = null,
            Build = context.State.Build is null ? null : context.State.Build with { BuildMethod = priority },
            Preferences = context.State.Preferences with
            {
                CharacterPriority = priority,
                KarmaNuyenRatio = karmaNuyenRatio,
                HouseRulesEnabled = houseRules
            },
            Notice = DesktopLocalizationCatalog.GetRequiredString(
                "desktop.dialog.character_settings.notice.updated",
                context.State.Preferences.Language)
        });
    }

    private static async Task ApplyMetadataDialogAsync(DesktopDialogState dialog, DialogCoordinationContext context, CancellationToken ct)
    {
        string? name = DesktopDialogFieldValueParser.GetValue(dialog, "metadataName");
        string? alias = DesktopDialogFieldValueParser.GetValue(dialog, "metadataAlias");
        string? notes = DesktopDialogFieldValueParser.GetValue(dialog, "metadataNotes");
        string? normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes;

        await context.UpdateMetadataAsync(new UpdateWorkspaceMetadata(
            Name: string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            Alias: string.IsNullOrWhiteSpace(alias) ? null : alias.Trim(),
            Notes: normalizedNotes), ct);

        CharacterOverviewState stateAfterUpdate = context.GetState();
        if (stateAfterUpdate.Error is null)
        {
            context.Publish(stateAfterUpdate with
            {
                ActiveDialog = null,
                Error = null,
                Notice = "Metadata updated."
            });
        }
    }

    private async Task RollDiceAsync(
        DesktopDialogState dialog,
        DialogCoordinationContext context,
        CancellationToken ct)
    {
        await Task.Yield();
        if (!TryReadDiceRequest(dialog, out DiceRollRequest request, out string error))
        {
            context.Publish(context.State with { Error = error, Notice = null });
            return;
        }

        DiceRollOutcome outcome = ExecuteDiceRoll(request);
        PublishDiceOutcome(dialog, context, outcome, "Dice roller updated.");
    }

    private static void RerollMisses(
        DesktopDialogState dialog,
        DialogCoordinationContext context)
    {
        string? lastRollStateJson = DesktopDialogFieldValueParser.GetValue(dialog, "diceLastRollState");
        if (string.IsNullOrWhiteSpace(lastRollStateJson))
        {
            context.Publish(context.State with
            {
                Error = "Roll the dice first before rerolling misses.",
                Notice = null
            });
            return;
        }

        DiceRollState? previous = JsonSerializer.Deserialize<DiceRollState>(lastRollStateJson);
        if (previous is null)
        {
            context.Publish(context.State with
            {
                Error = "The previous roll state could not be restored.",
                Notice = null
            });
            return;
        }

        int misses = previous.Lines.Count(line => !line.IsHit && !line.IsBubbleDie);
        if (misses <= 0)
        {
            context.Publish(context.State with
            {
                Error = null,
                Notice = "No misses are available to reroll."
            });
            return;
        }

        DiceRollOutcome outcome = ExecuteDiceReroll(previous);
        PublishDiceOutcome(dialog, context, outcome, "Misses rerolled.");
    }

    private static async Task CreateCharacterFromDialogAsync(
        DesktopDialogState dialog,
        DialogCoordinationContext context,
        CancellationToken ct)
    {
        string rulesetId = RulesetDefaults.NormalizeOptional(ReadDialogValue(dialog, "newCharacterRulesetId", RulesetDefaults.Sr5))
            ?? RulesetDefaults.Sr5;
        string name = ReadDialogValue(dialog, "newCharacterName", "New Character").Trim();
        string alias = ReadDialogValue(dialog, "newCharacterAlias", "Runner").Trim();
        string buildMethod = ReadDialogValue(dialog, "newCharacterBuildMethod", "Priority").Trim();

        if (string.IsNullOrWhiteSpace(alias))
        {
            alias = "Runner";
        }

        string xml = StarterWorkspaceXmlFactory.CreateCharacterXml(rulesetId, name, alias, buildMethod);
        await context.ImportAsync(
            new WorkspaceImportDocument(
                xml,
                rulesetId,
                WorkspaceDocumentFormat.NativeXml),
            ct);

        CharacterOverviewState stateAfterImport = context.GetState();
        if (stateAfterImport.Error is null)
        {
            context.Publish(stateAfterImport with
            {
                ActiveDialog = null,
                Error = null,
                Notice = $"Created '{name}' ({buildMethod}, {rulesetId.ToUpperInvariant()})."
            });
        }
    }

    private static bool TryReadDiceRequest(
        DesktopDialogState dialog,
        out DiceRollRequest request,
        out string error)
    {
        request = new DiceRollRequest(
            Method: "Standard",
            DiceCount: 1,
            Threshold: 0,
            Gremlins: 0,
            RuleOf6: false,
            CinematicGameplay: false,
            RushJob: false,
            VariableGlitch: false,
            BubbleDie: false);
        error = string.Empty;

        int diceCount = DesktopDialogFieldValueParser.ParseInt(dialog, "diceCount", 1);
        if (diceCount <= 0)
        {
            error = "Dice count must be greater than zero.";
            return false;
        }

        request = new DiceRollRequest(
            Method: ReadDialogValue(dialog, "diceMethod", "Standard"),
            DiceCount: diceCount,
            Threshold: Math.Max(0, DesktopDialogFieldValueParser.ParseInt(dialog, "diceThreshold", 0)),
            Gremlins: Math.Max(0, DesktopDialogFieldValueParser.ParseInt(dialog, "diceGremlins", 0)),
            RuleOf6: DesktopDialogFieldValueParser.ParseBool(dialog, "diceRuleOf6", false),
            CinematicGameplay: DesktopDialogFieldValueParser.ParseBool(dialog, "diceCinematicGameplay", false),
            RushJob: DesktopDialogFieldValueParser.ParseBool(dialog, "diceRushJob", false),
            VariableGlitch: DesktopDialogFieldValueParser.ParseBool(dialog, "diceVariableGlitch", false),
            BubbleDie: DesktopDialogFieldValueParser.ParseBool(dialog, "diceBubbleDie", false));
        return true;
    }

    private static DiceRollOutcome ExecuteDiceRoll(
        DiceRollRequest request)
    {
        Random random = CreateDiceRandom(request);
        List<DiceRollLine> lines = [];
        for (int index = 0; index < request.DiceCount; index++)
        {
            lines.Add(RollDiceLine(random, request, bubbleDie: false));
        }

        return FinalizeDiceOutcome(request, lines);
    }

    private static DiceRollOutcome ExecuteDiceReroll(
        DiceRollState previous)
    {
        Random random = CreateDiceRandom(previous.Request);
        List<DiceRollLine> carriedHits = previous.Lines
            .Where(line => line.IsHit && !line.IsBubbleDie)
            .Select(line => line with { Sequence = 0 })
            .ToList();
        int rerollCount = previous.Lines.Count(line => !line.IsHit && !line.IsBubbleDie);
        for (int index = 0; index < rerollCount; index++)
        {
            carriedHits.Add(RollDiceLine(random, previous.Request, bubbleDie: false));
        }

        return FinalizeDiceOutcome(previous.Request, carriedHits);
    }

    private static DiceRollOutcome FinalizeDiceOutcome(
        DiceRollRequest request,
        List<DiceRollLine> baseLines)
    {
        List<DiceRollLine> lines = baseLines
            .Select((line, index) => line with { Sequence = index + 1 })
            .ToList();

        int hitsWithoutBubble = CountDiceHits(lines, request.Method);
        int glitchCountWithoutBubble = lines.Count(line => line.IsGlitch);
        int glitchThresholdWithoutBubble = ComputeGlitchThreshold(lines.Count, hitsWithoutBubble, request.Gremlins, request.VariableGlitch);

        if (request.BubbleDie
            && (request.VariableGlitch
                || (glitchCountWithoutBubble == glitchThresholdWithoutBubble - 1
                    && (lines.Count & 1) == 0)))
        {
            Random bubbleRandom = CreateDiceRandom(request, bubbleSalt: 37);
            lines.Add(RollDiceLine(bubbleRandom, request, bubbleDie: true) with { Sequence = lines.Count + 1 });
        }

        int hits = CountDiceHits(lines, request.Method);
        int glitchCount = lines.Count(line => line.IsGlitch);
        int glitchThreshold = ComputeGlitchThreshold(lines.Count, hits, request.Gremlins, request.VariableGlitch);
        bool glitch = glitchCount >= glitchThreshold;
        bool criticalGlitch = glitch && hits == 0;
        int sum = lines.Sum(line => line.Total);

        string summary = BuildDiceSummary(request, hits, glitch, criticalGlitch, sum);
        string resultList = string.Join(
            Environment.NewLine,
            lines.Select(line => BuildDiceLineText(line, request.Method)));
        if (string.IsNullOrWhiteSpace(resultList))
        {
            resultList = "No rolls yet.";
        }

        string stateJson = JsonSerializer.Serialize(new DiceRollState(request, lines, glitch, criticalGlitch));
        return new DiceRollOutcome(summary, resultList, stateJson);
    }

    private static void PublishDiceOutcome(
        DesktopDialogState dialog,
        DialogCoordinationContext context,
        DiceRollOutcome outcome,
        string message)
    {
        List<DesktopDialogField> fields = dialog.Fields
            .Select(field => field.Id switch
            {
                "diceResultsSummary" => field with { Value = outcome.Summary, Placeholder = outcome.Summary },
                "diceResultsList" => field with { Value = outcome.ResultsList, Placeholder = outcome.ResultsList },
                "diceLastRollState" => field with { Value = outcome.StateJson, Placeholder = outcome.StateJson },
                _ => field
            })
            .ToList();

        context.Publish(context.State with
        {
            Error = null,
            Notice = outcome.Summary,
            ActiveDialog = dialog with
            {
                Message = message,
                Fields = fields
            }
        });
    }

    private static DiceRollLine RollDiceLine(
        Random random,
        DiceRollRequest request,
        bool bubbleDie)
    {
        int target = ResolveDiceTarget(request.Method, request.CinematicGameplay);
        int glitchMin = ResolveGlitchMinimum(request.Method, request.RushJob);
        bool allowRuleOf6 = string.Equals(request.Method, "Standard", StringComparison.OrdinalIgnoreCase) && request.RuleOf6;

        List<int> segments = [];
        int firstFace = 0;
        int current;
        do
        {
            current = random.Next(1, 7);
            if (segments.Count == 0)
            {
                firstFace = current;
            }

            segments.Add(current);
        } while (allowRuleOf6 && current == 6);

        int total = segments.Sum();
        bool hit = string.Equals(request.Method, "ReallyLarge", StringComparison.OrdinalIgnoreCase)
            ? total > 0
            : total >= target;
        bool glitch = firstFace <= glitchMin;
        return new DiceRollLine(0, segments, total, hit, glitch, bubbleDie);
    }

    private static string BuildDiceSummary(
        DiceRollRequest request,
        int hits,
        bool glitch,
        bool criticalGlitch,
        int sum)
    {
        string resultText = criticalGlitch
            ? "Critical Glitch"
            : glitch
                ? request.Threshold > 0
                    ? $"{(hits >= request.Threshold ? "Success" : "Failure")} (Glitch, {hits} hit{(hits == 1 ? string.Empty : "s")})"
                    : $"Glitch ({hits} hit{(hits == 1 ? string.Empty : "s")})"
                : request.Threshold > 0
                    ? $"{(hits >= request.Threshold ? "Success" : "Failure")} ({hits} hit{(hits == 1 ? string.Empty : "s")})"
                    : $"{hits} hit{(hits == 1 ? string.Empty : "s")}";
        return $"{resultText}{Environment.NewLine}{Environment.NewLine}Sum {sum}";
    }

    private static string BuildDiceLineText(DiceRollLine line, string method)
    {
        string rollText = string.Join("+", line.Segments);
        string marker = line.IsBubbleDie
            ? "bubble"
            : line.IsHit
                ? "hit"
                : "miss";
        string glitchMarker = line.IsGlitch ? " · glitch" : string.Empty;
        string prefix = line.IsBubbleDie ? "Bubble" : $"Die {line.Sequence}";
        return $"{prefix}: {rollText} = {line.Total} ({marker}{glitchMarker})";
    }

    private static int CountDiceHits(
        IReadOnlyList<DiceRollLine> lines,
        string method)
    {
        return string.Equals(method, "ReallyLarge", StringComparison.OrdinalIgnoreCase)
            ? lines.Sum(line => line.Total)
            : lines.Count(line => line.IsHit);
    }

    private static int ComputeGlitchThreshold(
        int diceCount,
        int hits,
        int gremlins,
        bool variableGlitch)
    {
        int threshold = variableGlitch
            ? hits + 1
            : (int)Math.Ceiling(diceCount / 2d);
        threshold -= gremlins;
        return Math.Max(1, threshold);
    }

    private static int ResolveDiceTarget(string method, bool cinematicGameplay)
    {
        if (string.Equals(method, "ReallyLarge", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(method, "Large", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return cinematicGameplay ? 4 : 5;
    }

    private static int ResolveGlitchMinimum(string method, bool rushJob)
    {
        if (string.Equals(method, "ReallyLarge", StringComparison.OrdinalIgnoreCase))
        {
            return 7;
        }

        return rushJob ? 2 : 1;
    }

    private static Random CreateDiceRandom(
        DiceRollRequest request,
        int bubbleSalt = 0)
    {
        int requestHash = HashCode.Combine(
            request.Method,
            request.DiceCount,
            request.Threshold,
            request.Gremlins,
            request.RuleOf6,
            request.CinematicGameplay,
            request.RushJob,
            request.VariableGlitch);
        int environmentHash = HashCode.Combine(
            request.BubbleDie,
            DateTime.UtcNow.Ticks,
            bubbleSalt);
        return new Random(HashCode.Combine(requestHash, environmentHash));
    }

    private sealed record DiceRollOutcome(
        string Summary,
        string ResultsList,
        string StateJson);

    private sealed record DiceRollState(
        DiceRollRequest Request,
        IReadOnlyList<DiceRollLine> Lines,
        bool WasGlitch,
        bool WasCriticalGlitch);

    private sealed record DiceRollRequest(
        string Method,
        int DiceCount,
        int Threshold,
        int Gremlins,
        bool RuleOf6,
        bool CinematicGameplay,
        bool RushJob,
        bool VariableGlitch,
        bool BubbleDie);

    private sealed record DiceRollLine(
        int Sequence,
        IReadOnlyList<int> Segments,
        int Total,
        bool IsHit,
        bool IsGlitch,
        bool IsBubbleDie);

    private static void PublishDialogNotice(
        DialogCoordinationContext context,
        string notice,
        DesktopPreferenceState? preferences = null)
    {
        context.Publish(context.State with
        {
            ActiveDialog = null,
            Error = null,
            Preferences = preferences ?? context.State.Preferences,
            Notice = notice
        });
    }

    private static void PublishRulesetAwareDialogNotice(
        DialogCoordinationContext context,
        string notice,
        DesktopPreferenceState? preferences = null)
        => PublishDialogNotice(context, RulesetUiDirectiveCatalog.FormatDialogNotice(ResolveContextRulesetId(context.State), notice), preferences);

    private static void PublishDialogAddMore(
        DialogCoordinationContext context,
        DesktopDialogState dialog,
        string notice,
        params string[] fieldIdsToReset)
    {
        DesktopDialogState resetDialog = dialog with
        {
            Fields = dialog.Fields
                .Select(field => fieldIdsToReset.Contains(field.Id, StringComparer.Ordinal)
                    ? field with { Value = field.Placeholder }
                    : field)
                .ToArray(),
            Message = "Previous selection added. Add & More keeps the classic selector open for the next entry."
        };

        resetDialog = DesktopDialogFactory.RebuildDynamicDialog(resetDialog, context.State.Preferences);

        context.Publish(context.State with
        {
            ActiveDialog = resetDialog,
            Error = null,
            Notice = notice
        });
    }

    private static void PublishRulesetAwareDialogAddMore(
        DialogCoordinationContext context,
        DesktopDialogState dialog,
        string notice,
        params string[] fieldIdsToReset)
        => PublishDialogAddMore(
            context,
            dialog,
            RulesetUiDirectiveCatalog.FormatDialogNotice(ResolveContextRulesetId(context.State), notice),
            fieldIdsToReset);

    private static async Task ApplyQuickAddDialogAsync(
        DialogCoordinationContext context,
        DesktopDialogState dialog,
        WorkspaceQuickAddRequest request,
        string notice,
        CancellationToken ct)
    {
        if (context.ApplyQuickAddAsync is null)
        {
            PublishRulesetAwareDialogNotice(context, notice);
            return;
        }

        await context.ApplyQuickAddAsync(request, ct);
        CharacterOverviewState stateAfterAdd = context.GetState();
        if (!string.IsNullOrWhiteSpace(stateAfterAdd.Error))
        {
            return;
        }

        context.Publish(stateAfterAdd with
        {
            ActiveDialog = null,
            Error = null,
            Notice = RulesetUiDirectiveCatalog.FormatDialogNotice(ResolveContextRulesetId(stateAfterAdd), notice)
        });
    }

    private static async Task ApplyQuickAddDialogAddMoreAsync(
        DialogCoordinationContext context,
        DesktopDialogState dialog,
        WorkspaceQuickAddRequest request,
        string notice,
        CancellationToken ct,
        params string[] fieldIdsToReset)
    {
        if (context.ApplyQuickAddAsync is null)
        {
            PublishRulesetAwareDialogAddMore(context, dialog, notice, fieldIdsToReset);
            return;
        }

        await context.ApplyQuickAddAsync(request, ct);
        CharacterOverviewState stateAfterAdd = context.GetState();
        if (!string.IsNullOrWhiteSpace(stateAfterAdd.Error))
        {
            return;
        }

        DesktopDialogState resetDialog = dialog with
        {
            Fields = dialog.Fields
                .Select(field => fieldIdsToReset.Contains(field.Id, StringComparer.Ordinal)
                    ? field with { Value = field.Placeholder }
                    : field)
                .ToArray(),
            Message = "Previous selection added. Add & More keeps the classic selector open for the next entry."
        };

        resetDialog = DesktopDialogFactory.RebuildDynamicDialog(resetDialog, stateAfterAdd.Preferences);

        context.Publish(stateAfterAdd with
        {
            ActiveDialog = resetDialog,
            Error = null,
            Notice = RulesetUiDirectiveCatalog.FormatDialogNotice(ResolveContextRulesetId(stateAfterAdd), notice)
        });
    }

    private static WorkspaceQuickAddRequest BuildGearQuickAddRequest(DesktopDialogState dialog)
    {
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.Gear,
            Name: ReadDialogValue(dialog, "uiGearName", "Gear"),
            Category: ResolveSelectionCategory(dialog, "uiGearSelectedBranch", "uiGearCategory", "Gear"),
            Source: ReadDialogValue(dialog, "uiGearSource", "Desktop Quick Add"),
            Cost: ReadDialogValue(dialog, "uiGearCost", "0"),
            Rating: DesktopDialogFieldValueParser.ParseInt(dialog, "uiGearRating", 0),
            Quantity: Math.Max(1, DesktopDialogFieldValueParser.ParseInt(dialog, "uiGearQuantity", 1)));
    }

    private static WorkspaceQuickAddRequest BuildWeaponQuickAddRequest(DesktopDialogState dialog)
    {
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.Weapon,
            Name: ReadDialogValue(dialog, "uiWeaponName", "Weapon"),
            Category: ResolveSelectionCategory(dialog, "uiWeaponSelectedBranch", "uiWeaponCategory", "Weapon"),
            Source: ReadDialogValue(dialog, "uiWeaponSource", "Desktop Quick Add"),
            Cost: ReadDialogValue(dialog, "uiWeaponCost", "0"),
            Accuracy: ReadDialogValue(dialog, "uiWeaponAccuracy", "4"));
    }

    private static WorkspaceQuickAddRequest BuildArmorQuickAddRequest(DesktopDialogState dialog)
    {
        string armorValue = ReadDialogValue(dialog, "uiArmorRating", "0");
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.Armor,
            Name: ReadDialogValue(dialog, "uiArmorName", "Armor"),
            Category: ResolveSelectionCategory(dialog, "uiArmorSelectedBranch", "uiArmorCategory", "Armor"),
            Source: ReadDialogValue(dialog, "uiArmorSource", "Desktop Quick Add"),
            Cost: ReadDialogValue(dialog, "uiArmorCost", "0"),
            ArmorValue: armorValue,
            Rating: DesktopDialogFieldValueParser.ParseInt(dialog, "uiArmorRating", 0));
    }

    private static WorkspaceQuickAddRequest BuildSkillQuickAddRequest(DesktopDialogState dialog)
    {
        string category = ReadDialogValue(dialog, "uiSkillCategory", "Active");
        bool isKnowledge = category.Contains("knowledge", StringComparison.OrdinalIgnoreCase)
            || category.Contains("language", StringComparison.OrdinalIgnoreCase);
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.Skill,
            Name: ReadDialogValue(dialog, "uiSkillName", "Skill"),
            Category: isKnowledge ? "Knowledge" : "Active Skill",
            BaseValue: Math.Max(1, DesktopDialogFieldValueParser.ParseInt(dialog, "uiSkillRating", 1)),
            IsKnowledge: isKnowledge);
    }

    private static WorkspaceQuickAddRequest BuildContactQuickAddRequest(DesktopDialogState dialog)
    {
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.Contact,
            Name: ReadDialogValue(dialog, "uiContactName", "Contact"),
            Role: ReadDialogValue(dialog, "uiContactRole", "Contact"),
            Location: "Seattle",
            Connection: Math.Max(0, DesktopDialogFieldValueParser.ParseInt(dialog, "uiContactConnection", 0)),
            Loyalty: Math.Max(0, DesktopDialogFieldValueParser.ParseInt(dialog, "uiContactLoyalty", 0)));
    }

    private static WorkspaceQuickAddRequest BuildVehicleQuickAddRequest(DesktopDialogState dialog)
    {
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.Vehicle,
            Name: ReadDialogValue(dialog, "uiVehicleName", "Vehicle"),
            Category: ResolveSelectionCategory(dialog, "uiVehicleSelectedBranch", "uiVehicleCategory", "Vehicle"),
            Source: ReadDialogValue(dialog, "uiVehicleSource", "Desktop Quick Add"),
            Cost: ReadDialogValue(dialog, "uiVehicleCost", "0"),
            Handling: ReadDialogValue(dialog, "uiVehicleHandling", "3"),
            Speed: ReadDialogValue(dialog, "uiVehicleSpeed", "3"),
            Body: ReadDialogValue(dialog, "uiVehicleBody", "10"),
            ArmorValue: ReadDialogValue(dialog, "uiVehicleArmor", "8"),
            Sensor: "2",
            Seats: "4");
    }

    private static WorkspaceQuickAddRequest BuildQualityQuickAddRequest(DesktopDialogState dialog)
    {
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.Quality,
            Name: ReadDialogValue(dialog, "uiQualityName", "Quality"),
            Category: ReadDialogValue(dialog, "uiQualityType", "Positive"),
            Source: "Desktop Quick Add",
            Karma: DesktopDialogFieldValueParser.ParseInt(dialog, "uiQualityKarma", 0));
    }

    private static WorkspaceQuickAddRequest BuildDrugQuickAddRequest(DesktopDialogState dialog)
    {
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.Drug,
            Name: ReadDialogValue(dialog, "uiDrugName", "Drug"),
            Category: "Drug",
            Source: ReadDialogValue(dialog, "uiDrugSource", "Desktop Quick Add"),
            Quantity: Math.Max(1, DesktopDialogFieldValueParser.ParseInt(dialog, "uiDrugQuantity", 1)));
    }

    private static WorkspaceQuickAddRequest BuildCyberwareQuickAddRequest(DesktopDialogState dialog)
    {
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.Cyberware,
            Name: ReadDialogValue(dialog, "uiCyberwareName", "Cyberware"),
            Category: ResolveSelectionCategory(dialog, "uiCyberwareSelectedBranch", "uiCyberwareCategory", "Cyberware"),
            Source: ReadDialogValue(dialog, "uiCyberwareSource", "Desktop Quick Add"),
            Cost: ReadDialogValue(dialog, "uiCyberwareCost", "0"),
            Rating: Math.Max(0, DesktopDialogFieldValueParser.ParseInt(dialog, "uiCyberwareRating", 0)),
            Grade: ReadDialogValue(dialog, "uiCyberwareGrade", "Standard"),
            Essence: ReadDialogValue(dialog, "uiCyberwareEssence", "0.00"),
            Capacity: ReadDialogValue(dialog, "uiCyberwareCapacity", "n/a"),
            Location: ReadDialogValue(dialog, "uiCyberwareSlot", "Body"));
    }

    private static WorkspaceQuickAddRequest BuildMagicQuickAddRequest(DesktopDialogState dialog)
    {
        string family = ReadDialogValue(dialog, "uiMagicFamily", "Spell");
        string normalizedFamily = family.Trim();
        string kind = normalizedFamily.Contains("complex", StringComparison.OrdinalIgnoreCase)
            ? WorkspaceQuickAddKinds.ComplexForm
            : normalizedFamily.Contains("adept", StringComparison.OrdinalIgnoreCase)
                || normalizedFamily.Contains("power", StringComparison.OrdinalIgnoreCase)
                ? WorkspaceQuickAddKinds.Power
                : WorkspaceQuickAddKinds.Spell;

        return kind switch
        {
            WorkspaceQuickAddKinds.ComplexForm => new WorkspaceQuickAddRequest(
                Kind: WorkspaceQuickAddKinds.ComplexForm,
                Name: ReadDialogValue(dialog, "uiMagicName", "Complex Form"),
                Source: ReadDialogValue(dialog, "uiMagicSource", "Desktop Quick Add"),
                Target: ReadDialogValue(dialog, "uiMagicCategory", "Persona"),
                Duration: "Sustained",
                FadingValue: $"Level {Math.Max(1, DesktopDialogFieldValueParser.ParseInt(dialog, "uiMagicLevel", 1))}"),
            WorkspaceQuickAddKinds.Power => new WorkspaceQuickAddRequest(
                Kind: WorkspaceQuickAddKinds.Power,
                Name: ReadDialogValue(dialog, "uiMagicName", "Power"),
                Source: ReadDialogValue(dialog, "uiMagicSource", "Desktop Quick Add"),
                Rating: Math.Max(1, DesktopDialogFieldValueParser.ParseInt(dialog, "uiMagicLevel", 1)),
                PointsPerLevel: ParseDecimalOrDefault(ReadDialogValue(dialog, "uiMagicLevel", "1"), 1m)),
            _ => new WorkspaceQuickAddRequest(
                Kind: WorkspaceQuickAddKinds.Spell,
                Name: ReadDialogValue(dialog, "uiMagicName", "Spell"),
                Category: ReadDialogValue(dialog, "uiMagicCategory", "Combat"),
                Source: ReadDialogValue(dialog, "uiMagicSource", "Desktop Quick Add"),
                Type: "Mana",
                Range: "LOS",
                Duration: "Instant",
                DrainValue: "F-3")
        };
    }

    private static WorkspaceQuickAddRequest BuildSpellQuickAddRequest(DesktopDialogState dialog)
    {
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.Spell,
            Name: ReadDialogValue(dialog, "uiSpellName", "Spell"),
            Category: ReadDialogValue(dialog, "uiSpellCategory", "Combat"),
            Source: ReadDialogValue(dialog, "uiSpellSource", "Desktop Quick Add"),
            Type: "Mana",
            Range: "LOS",
            Duration: "Instant",
            DrainValue: "F-3");
    }

    private static WorkspaceQuickAddRequest BuildAdeptPowerQuickAddRequest(DesktopDialogState dialog)
    {
        int rating = Math.Max(1, DesktopDialogFieldValueParser.ParseInt(dialog, "uiAdeptPowerLevel", 1));
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.Power,
            Name: ReadDialogValue(dialog, "uiAdeptPowerName", "Adept Power"),
            Source: ReadDialogValue(dialog, "uiAdeptPowerSource", "Desktop Quick Add"),
            Rating: rating,
            PointsPerLevel: ParseDecimalOrDefault(ReadDialogValue(dialog, "uiAdeptPowerLevel", "1"), 1m));
    }

    private static WorkspaceQuickAddRequest BuildComplexFormQuickAddRequest(DesktopDialogState dialog)
    {
        int level = Math.Max(1, DesktopDialogFieldValueParser.ParseInt(dialog, "uiComplexFormLevel", 1));
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.ComplexForm,
            Name: ReadDialogValue(dialog, "uiComplexFormName", "Complex Form"),
            Source: ReadDialogValue(dialog, "uiComplexFormSource", "Desktop Quick Add"),
            Target: "Persona",
            Duration: "Sustained",
            FadingValue: $"Level {level}");
    }

    private static WorkspaceQuickAddRequest BuildMatrixProgramQuickAddRequest(DesktopDialogState dialog)
    {
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.MatrixProgram,
            Name: ReadDialogValue(dialog, "uiMatrixProgramName", "Program"),
            Source: ReadDialogValue(dialog, "uiMatrixProgramSource", "Desktop Quick Add"),
            Slot: ReadDialogValue(dialog, "uiMatrixProgramSlot", "Common"));
    }

    private static WorkspaceQuickAddRequest BuildInitiationQuickAddRequest(DesktopDialogState dialog)
    {
        string track = ReadDialogValue(dialog, "uiInitiationTrack", "Initiation");
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.InitiationGrade,
            Name: ReadDialogValue(dialog, "uiInitiationReward", "Initiation Reward"),
            Rating: Math.Max(1, DesktopDialogFieldValueParser.ParseInt(dialog, "uiInitiationGrade", 1)),
            Res: track.Contains("submersion", StringComparison.OrdinalIgnoreCase),
            Reward: ReadDialogValue(dialog, "uiInitiationReward", "Initiation Reward"));
    }

    private static WorkspaceQuickAddRequest BuildSpiritQuickAddRequest(DesktopDialogState dialog)
    {
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.Spirit,
            Name: ReadDialogValue(dialog, "uiSpiritName", "Spirit"),
            Category: ReadDialogValue(dialog, "uiSpiritType", "Spirit"),
            Force: Math.Max(1, DesktopDialogFieldValueParser.ParseInt(dialog, "uiSpiritForce", 1)),
            Services: 0,
            Bound: false);
    }

    private static WorkspaceQuickAddRequest BuildCritterPowerQuickAddRequest(DesktopDialogState dialog)
    {
        return new WorkspaceQuickAddRequest(
            Kind: WorkspaceQuickAddKinds.CritterPower,
            Name: ReadDialogValue(dialog, "uiCritterPowerName", "Critter Power"),
            Category: "Critter Power",
            Type: "Passive",
            Source: "Desktop Quick Add",
            Range: "Self",
            Duration: "Always",
            Rating: Math.Max(1, DesktopDialogFieldValueParser.ParseInt(dialog, "uiCritterPowerRating", 1)));
    }

    private static decimal ParseDecimalOrDefault(string? value, decimal fallback)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : fallback;

    private static string ResolveSelectionCategory(
        DesktopDialogState dialog,
        string branchFieldId,
        string categoryFieldId,
        string fallback)
    {
        string branch = ReadDialogValue(dialog, branchFieldId, string.Empty);
        if (!string.IsNullOrWhiteSpace(branch))
        {
            return branch;
        }

        string category = ReadDialogValue(dialog, categoryFieldId, fallback);
        return IsShowAllSelectionCategory(category) ? fallback : category;
    }

    private static bool TryCoordinateLegacyDeleteAction(
        DesktopDialogState dialog,
        string actionId,
        DialogCoordinationContext context)
    {
        if (!string.Equals(actionId, "delete", StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryGetLegacyDeleteNotice(dialog, out string notice))
        {
            return false;
        }

        PublishRulesetAwareDialogNotice(context, notice);
        return true;
    }

    private static bool TryGetLegacyDeleteNotice(DesktopDialogState dialog, out string notice)
    {
        string target = ReadDialogValue(dialog, "uiDeleteTarget", "Selected Entry");

        switch (dialog.Id)
        {
            case "dialog.ui.delete_entry":
                notice = $"Entry '{target}' removed.";
                return true;
            case "dialog.ui.gear_delete":
                notice = $"Gear '{target}' removed.";
                return true;
            case "dialog.ui.cyberware_delete":
                notice = $"Cyberware '{target}' removed.";
                return true;
            case "dialog.ui.drug_delete":
                notice = $"Drug '{target}' removed.";
                return true;
            case "dialog.ui.magic_delete":
                notice = $"Spell/power '{target}' removed.";
                return true;
            case "dialog.ui.skill_remove":
                notice = $"Skill '{target}' removed.";
                return true;
            case "dialog.ui.vehicle_delete":
                notice = $"Vehicle '{target}' removed.";
                return true;
            case "dialog.ui.contact_remove":
                notice = $"Contact '{target}' removed.";
                return true;
            case "dialog.ui.quality_delete":
                notice = $"Quality '{target}' removed.";
                return true;
            default:
                notice = string.Empty;
                return false;
        }
    }

    private static bool TryCoordinateLegacySelectionAction(
        DesktopDialogState dialog,
        string actionId,
        DialogCoordinationContext context)
    {
        if (!TryGetLegacySelectionDialogConfig(dialog.Id, out LegacySelectionDialogConfig config))
        {
            return false;
        }

        switch (actionId)
        {
            case "focus_category":
            {
                string currentCategory = ReadDialogValue(dialog, config.CategoryFieldId, "Show All");
                string selectedBranch = ReadDialogValue(dialog, config.SelectedBranchFieldId, currentCategory);
                string nextCategory = IsShowAllSelectionCategory(currentCategory) && !string.IsNullOrWhiteSpace(selectedBranch)
                    ? selectedBranch
                    : "Show All";

                PublishLegacySelectionDialog(
                    context,
                    UpdateDialogField(dialog, config.CategoryFieldId, nextCategory),
                    $"Category focus changed to '{nextCategory}'.");
                return true;
            }
            case "toggle_search_scope":
            {
                bool currentScope = DesktopDialogFieldValueParser.ParseBool(dialog, config.SearchScopeFieldId, true);
                bool nextScope = !currentScope;
                PublishLegacySelectionDialog(
                    context,
                    UpdateDialogField(dialog, config.SearchScopeFieldId, nextScope ? "true" : "false"),
                    nextScope
                        ? "Search scope changed to the current category."
                        : "Search scope changed to all categories.");
                return true;
            }
            default:
                return false;
        }
    }

    private static bool TryGetLegacySelectionDialogConfig(
        string dialogId,
        out LegacySelectionDialogConfig config)
    {
        config = dialogId switch
        {
            "dialog.ui.cyberware_add" => new LegacySelectionDialogConfig("uiCyberwareCategory", "uiCyberwareSearchInCategoryOnly", "uiCyberwareSelectedBranch"),
            "dialog.ui.gear_add" => new LegacySelectionDialogConfig("uiGearCategory", "uiGearSearchInCategoryOnly", "uiGearSelectedBranch"),
            "dialog.ui.combat_add_weapon" => new LegacySelectionDialogConfig("uiWeaponCategory", "uiWeaponSearchInCategoryOnly", "uiWeaponSelectedBranch"),
            "dialog.ui.combat_add_armor" => new LegacySelectionDialogConfig("uiArmorCategory", "uiArmorSearchInCategoryOnly", "uiArmorSelectedBranch"),
            "dialog.ui.vehicle_add" => new LegacySelectionDialogConfig("uiVehicleCategory", "uiVehicleSearchInCategoryOnly", "uiVehicleSelectedBranch"),
            _ => new LegacySelectionDialogConfig(string.Empty, string.Empty, string.Empty)
        };

        return !string.IsNullOrWhiteSpace(config.CategoryFieldId);
    }

    private static DesktopDialogState UpdateDialogField(
        DesktopDialogState dialog,
        string fieldId,
        string value)
    {
        return dialog with
        {
            Fields = dialog.Fields
                .Select(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal)
                    ? field with { Value = value, Placeholder = value }
                    : field)
                .ToArray()
        };
    }

    private static bool IsShowAllSelectionCategory(string? category)
        => string.IsNullOrWhiteSpace(category)
            || string.Equals(category, "All", StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, "Show All", StringComparison.OrdinalIgnoreCase);

    private static void PublishLegacySelectionDialog(
        DialogCoordinationContext context,
        DesktopDialogState dialog,
        string notice)
    {
        CharacterOverviewState state = context.GetState();
        DesktopDialogState rebuiltDialog = DesktopDialogFactory.RebuildDynamicDialog(dialog, state.Preferences);
        context.Publish(state with
        {
            ActiveDialog = rebuiltDialog,
            Error = null,
            Notice = RulesetUiDirectiveCatalog.FormatDialogNotice(ResolveContextRulesetId(state), notice)
        });
    }

    private static void RefreshCharacterRosterDialog(
        DesktopDialogState dialog,
        DialogCoordinationContext context)
    {
        string rosterPath = DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderPath")
            ?? context.State.Preferences.CharacterRosterPath;
        bool folderConfigured = !string.IsNullOrWhiteSpace(rosterPath);
        bool folderExisted = folderConfigured && Directory.Exists(rosterPath);

        if (folderConfigured && !folderExisted)
        {
            Directory.CreateDirectory(rosterPath);
        }

        string notice = !folderConfigured
            ? "Set Character Roster Path in Global Settings before refreshing the roster watch folder."
            : folderExisted
                ? "Roster watch folder refreshed."
                : $"Roster folder created and refreshed at '{rosterPath}'.";

        PublishCharacterRosterDialog(context, notice);
    }

    private static void OpenCharacterRosterRunner(
        DesktopDialogState dialog,
        DialogCoordinationContext context)
    {
        string runnerId = DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunnerId") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(runnerId))
        {
            PublishCharacterRosterDialog(context, "No runner is currently selected in the roster.");
            return;
        }

        OpenWorkspaceState? selectedRunner = context.State.OpenWorkspaces
            .FirstOrDefault(workspace => string.Equals(workspace.Id.Value, runnerId, StringComparison.Ordinal));
        if (selectedRunner is null)
        {
            PublishCharacterRosterDialog(context, $"Selected roster runner '{runnerId}' is not available in the current workbench session.");
            return;
        }

        context.Publish(context.State with
        {
            WorkspaceId = selectedRunner.Id,
            ActiveDialog = null,
            Error = null,
            Notice = RulesetUiDirectiveCatalog.FormatDialogNotice(
                RulesetDefaults.NormalizeOptional(selectedRunner.RulesetId),
                $"Runner '{selectedRunner.Alias}' opened from roster.")
        });
    }

    private static void OpenCharacterRosterWatchFile(
        DesktopDialogState dialog,
        DialogCoordinationContext context)
    {
        string selectedWatchFile = DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedWatchFile") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(selectedWatchFile))
        {
            PublishCharacterRosterDialog(context, "No watched runner file is currently matched.");
            return;
        }

        CharacterOverviewState state = context.GetState();
        string watchFileStem = Path.GetFileNameWithoutExtension(selectedWatchFile);
        string normalizedWatchFileStem = NormalizeCharacterRosterWatchToken(watchFileStem);
        OpenWorkspaceState? matchedRunner = state.OpenWorkspaces.FirstOrDefault(workspace =>
            string.Equals(NormalizeCharacterRosterWatchToken(workspace.Id.Value), normalizedWatchFileStem, StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeCharacterRosterWatchToken(workspace.Alias), normalizedWatchFileStem, StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeCharacterRosterWatchToken(workspace.Name), normalizedWatchFileStem, StringComparison.OrdinalIgnoreCase));

        if (matchedRunner is null)
        {
            PublishCharacterRosterDialog(context, $"Watch file '{selectedWatchFile}' surfaced in the roster workbench.");
            return;
        }

        if (state.WorkspaceId is not null && string.Equals(state.WorkspaceId.Value.Value, matchedRunner.Id.Value, StringComparison.Ordinal))
        {
            PublishCharacterRosterDialog(context, $"Watch file '{selectedWatchFile}' is already aligned with runner '{matchedRunner.Alias}'.");
            return;
        }

        context.Publish(state with
        {
            WorkspaceId = matchedRunner.Id,
            ActiveDialog = null,
            Error = null,
            Notice = RulesetUiDirectiveCatalog.FormatDialogNotice(
                RulesetDefaults.NormalizeOptional(matchedRunner.RulesetId),
                $"Watched runner '{matchedRunner.Alias}' opened from roster watch folder.")
        });
    }

    private static void OpenCharacterRosterFolder(
        DesktopDialogState dialog,
        DialogCoordinationContext context)
    {
        string rosterPath = DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderPath")
            ?? context.State.Preferences.CharacterRosterPath;
        if (string.IsNullOrWhiteSpace(rosterPath))
        {
            PublishCharacterRosterDialog(context, "Set Character Roster Path in Global Settings before opening the roster folder.");
            return;
        }

        bool folderExisted = Directory.Exists(rosterPath);
        if (!folderExisted)
        {
            Directory.CreateDirectory(rosterPath);
        }

        PublishCharacterRosterDialog(
            context,
            folderExisted
                ? $"Roster folder ready at '{rosterPath}'."
                : $"Roster folder created at '{rosterPath}'.");
    }

    private static void PublishCharacterRosterCommandNotice(
        DesktopDialogState dialog,
        DialogCoordinationContext context,
        string fieldId,
        string emptyNotice,
        Func<string, string> noticeFactory)
    {
        string selectedValue = DesktopDialogFieldValueParser.GetValue(dialog, fieldId) ?? string.Empty;
        PublishCharacterRosterDialog(
            context,
            string.IsNullOrWhiteSpace(selectedValue) ? emptyNotice : noticeFactory(selectedValue));
    }

    private static void PublishCharacterRosterDialog(
        DialogCoordinationContext context,
        string notice)
    {
        DesktopDialogFactory dialogFactory = new();
        CharacterOverviewState state = context.GetState();
        DesktopDialogState rosterDialog = dialogFactory.CreateCommandDialog(
            "character_roster",
            state.Profile,
            state.Preferences,
            state.ActiveSectionJson,
            state.WorkspaceId,
            ResolveContextRulesetId(state),
            openWorkspaces: state.OpenWorkspaces);

        context.Publish(state with
        {
            ActiveDialog = rosterDialog,
            Error = null,
            Notice = notice
        });
    }

    private static void PublishMasterIndexDialog(
        DialogCoordinationContext context,
        DesktopDialogState dialog,
        string notice)
    {
        CharacterOverviewState state = context.GetState();
        DesktopDialogState rebuiltDialog = DesktopDialogFactory.RebuildDynamicDialog(dialog, state.Preferences);
        context.Publish(state with
        {
            ActiveDialog = rebuiltDialog,
            Error = null,
            Notice = notice
        });
    }

    private static void CycleMasterIndexFile(
        DesktopDialogState dialog,
        DialogCoordinationContext context)
    {
        MasterIndexCoordinatorSnapshot? snapshot = ReadMasterIndexSnapshot(dialog);
        if (snapshot is null)
        {
            PublishMasterIndexDialog(context, dialog, "Master Index file filter remains unresolved.");
            return;
        }

        string[] files = ["All", .. snapshot.Files.Select(file => file.File).Where(file => !string.IsNullOrWhiteSpace(file)).Distinct(StringComparer.OrdinalIgnoreCase)];
        string currentFile = ReadDialogValue(dialog, "masterIndexActiveFile", "All");
        int currentIndex = Array.FindIndex(files, file => string.Equals(file, currentFile, StringComparison.OrdinalIgnoreCase));
        int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % files.Length;
        string nextFile = files[nextIndex];

        DesktopDialogState updatedDialog = dialog with
        {
            Fields = dialog.Fields
                .Select(field => field.Id switch
                {
                    "masterIndexActiveFile" => field with { Value = nextFile, Placeholder = nextFile },
                    "masterIndexActiveResultKey" => field with { Value = string.Empty, Placeholder = string.Empty },
                    _ => field
                })
                .ToArray()
        };

        PublishMasterIndexDialog(
            context,
            updatedDialog,
            $"Master Index file filter changed to '{nextFile}'.");
    }

    private static void CycleMasterIndexSourcebook(
        DesktopDialogState dialog,
        DialogCoordinationContext context)
    {
        MasterIndexCoordinatorSnapshot? snapshot = ReadMasterIndexSnapshot(dialog);
        if (snapshot is null || snapshot.Sourcebooks.Count == 0)
        {
            PublishMasterIndexDialog(context, dialog, "Master Index sourcebook remains unresolved.");
            return;
        }

        string currentId = ReadDialogValue(dialog, "masterIndexActiveSourcebookId", snapshot.Sourcebooks[0].Id);
        string currentFile = ReadDialogValue(dialog, "masterIndexActiveFile", "All");
        int currentIndex = snapshot.Sourcebooks.FindIndex(sourcebook => string.Equals(sourcebook.Id, currentId, StringComparison.Ordinal));
        int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % snapshot.Sourcebooks.Count;
        MasterIndexCoordinatorSourcebookSnapshot nextSourcebook = snapshot.Sourcebooks[nextIndex];
        string nextFile = ResolveMasterIndexSourcebookFileSelection(nextSourcebook, currentFile);

        DesktopDialogState updatedDialog = dialog with
        {
            Fields = dialog.Fields
                .Select(field => field.Id switch
                {
                    "masterIndexActiveSourcebookId" => field with { Value = nextSourcebook.Id, Placeholder = nextSourcebook.Id },
                    "masterIndexCurrentSourcebook" => field with { Value = $"{nextSourcebook.Code} · {nextSourcebook.Name}", Placeholder = $"{nextSourcebook.Code} · {nextSourcebook.Name}" },
                    "masterIndexActiveFile" => field with { Value = nextFile, Placeholder = nextFile },
                    "masterIndexActiveResultKey" => field with { Value = string.Empty, Placeholder = string.Empty },
                    _ => field
                })
                .ToArray()
        };

        PublishMasterIndexDialog(
            context,
            updatedDialog,
            string.Equals(nextFile, currentFile, StringComparison.OrdinalIgnoreCase)
                ? $"Master Index sourcebook changed to '{nextSourcebook.Code} · {nextSourcebook.Name}'."
                : $"Master Index sourcebook changed to '{nextSourcebook.Code} · {nextSourcebook.Name}' and data file changed to '{nextFile}'.");
    }

    private static string ResolveMasterIndexSourcebookFileSelection(
        MasterIndexCoordinatorSourcebookSnapshot sourcebook,
        string currentFile)
    {
        if (string.IsNullOrWhiteSpace(currentFile) || string.Equals(currentFile, "All", StringComparison.OrdinalIgnoreCase))
        {
            return SelectFirstMasterIndexSourcebookFile(sourcebook) ?? "All";
        }

        bool currentFileHasEntries = sourcebook.RuleSnippets.Any(snippet =>
            string.Equals(snippet.File, currentFile, StringComparison.OrdinalIgnoreCase)
            || string.Equals(snippet.Provenance, currentFile, StringComparison.OrdinalIgnoreCase));

        return currentFileHasEntries
            ? currentFile
            : SelectFirstMasterIndexSourcebookFile(sourcebook) ?? "All";
    }

    private static string? SelectFirstMasterIndexSourcebookFile(MasterIndexCoordinatorSourcebookSnapshot sourcebook)
    {
        return sourcebook.RuleSnippets
            .Select(snippet => !string.IsNullOrWhiteSpace(snippet.File) ? snippet.File : snippet.Provenance)
            .FirstOrDefault(file => !string.IsNullOrWhiteSpace(file));
    }

    private static MasterIndexCoordinatorSnapshot? ReadMasterIndexSnapshot(DesktopDialogState dialog)
    {
        string snapshotJson = DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSnapshot") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<MasterIndexCoordinatorSnapshot>(snapshotJson);
    }

    private static string? ResolveContextRulesetId(CharacterOverviewState state)
    {
        if (state.WorkspaceId is not null)
        {
            string? activeWorkspaceRulesetId = state.OpenWorkspaces
                .Where(workspace => string.Equals(workspace.Id.Value, state.WorkspaceId.Value.Value, StringComparison.Ordinal))
                .Select(workspace => RulesetDefaults.NormalizeOptional(workspace.RulesetId))
                .FirstOrDefault(rulesetId => !string.IsNullOrWhiteSpace(rulesetId));

            if (!string.IsNullOrWhiteSpace(activeWorkspaceRulesetId))
            {
                return activeWorkspaceRulesetId;
            }
        }

        return state.OpenWorkspaces
            .Select(workspace => RulesetDefaults.NormalizeOptional(workspace.RulesetId))
            .Concat(state.Commands.Select(command => RulesetDefaults.NormalizeOptional(command.RulesetId)))
            .Concat(state.NavigationTabs.Select(tab => RulesetDefaults.NormalizeOptional(tab.RulesetId)))
            .FirstOrDefault(rulesetId => !string.IsNullOrWhiteSpace(rulesetId));
    }

    private static string ReadDialogValue(
        DesktopDialogState dialog,
        string fieldId,
        string fallback)
    {
        string? value = DesktopDialogFieldValueParser.GetValue(dialog, fieldId);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeCharacterRosterWatchToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] normalized = value
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_'
                ? char.ToLowerInvariant(character)
                : '-')
            .ToArray();

        return new string(normalized).Trim('-');
    }

    private sealed record MasterIndexCoordinatorSnapshot(
        string SettingsLanePosture,
        List<MasterIndexCoordinatorFileSnapshot> Files,
        List<MasterIndexCoordinatorSourcebookSnapshot> Sourcebooks);

    private sealed record LegacySelectionDialogConfig(
        string CategoryFieldId,
        string SearchScopeFieldId,
        string SelectedBranchFieldId);

    private sealed record MasterIndexCoordinatorFileSnapshot(
        string File,
        int ElementCount);

    private sealed record MasterIndexCoordinatorSourcebookSnapshot(
        string Id,
        string Code,
        string Name,
        string ReferencePosture,
        string ReferenceSourcePosture,
        string? LocalPdfPath,
        string? ReferenceUrl,
        string? ReferenceSnapshot,
        List<MasterIndexCoordinatorSnippetSnapshot> RuleSnippets);

    private sealed record MasterIndexCoordinatorSnippetSnapshot(
        string File,
        string Provenance,
        int Page,
        string Snippet);

}
