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

        if (string.Equals(dialog.Id, "dialog.dice_roller", StringComparison.Ordinal) && string.Equals(actionId, "derive_initiative", StringComparison.Ordinal))
        {
            DeriveInitiativePreview(dialog, context);
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
                    PublishCharacterRosterCommandNotice(dialog, context, "rosterSelectedWatchFile", "No watched runner file is currently matched.", relativePath => $"Watch file '{relativePath}' surfaced in the roster workbench.");
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
                case "edit_setting":
                    OpenCharacterSettingsFromMasterIndex(context);
                    return;
            }
        }

        if (string.Equals(dialog.Id, "dialog.ui.open_notes", StringComparison.Ordinal) && string.Equals(actionId, "save", StringComparison.Ordinal))
        {
            string notes = DesktopDialogFieldValueParser.GetValue(dialog, "uiNotesEditor") ?? string.Empty;
            PublishDialogNotice(context, "Notes saved.", context.State.Preferences with
            {
                CharacterNotes = notes
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

        if (string.Equals(dialog.Id, "dialog.ui.delete_entry", StringComparison.Ordinal) && string.Equals(actionId, "delete", StringComparison.Ordinal))
        {
            PublishRulesetAwareDialogNotice(context, "Entry deleted.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.gear_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string gearName = ReadDialogValue(dialog, "uiGearName", "Ares Predator");
            PublishRulesetAwareDialogNotice(context, $"Gear '{gearName}' added.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.gear_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string gearName = ReadDialogValue(dialog, "uiGearName", "Ares Predator");
            PublishRulesetAwareDialogAddMore(context, dialog, $"Gear '{gearName}' added. Dialog remains open for another item.", "uiGearQuantity", "uiGearMarkup", "uiGearFreeItem");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.gear_edit", StringComparison.Ordinal) && string.Equals(actionId, "apply", StringComparison.Ordinal))
        {
            string gearName = ReadDialogValue(dialog, "uiGearEditName", "Selected Gear");
            PublishRulesetAwareDialogNotice(context, $"Gear renamed to '{gearName}'.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.gear_delete", StringComparison.Ordinal) && string.Equals(actionId, "delete", StringComparison.Ordinal))
        {
            PublishRulesetAwareDialogNotice(context, "Gear deleted.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.cyberware_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string cyberwareName = ReadDialogValue(dialog, "uiCyberwareName", "Wired Reflexes 2");
            PublishRulesetAwareDialogNotice(context, $"Cyberware '{cyberwareName}' added.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.cyberware_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string cyberwareName = ReadDialogValue(dialog, "uiCyberwareName", "Wired Reflexes 2");
            PublishRulesetAwareDialogAddMore(context, dialog, $"Cyberware '{cyberwareName}' added. Dialog remains open for another implant.", "uiCyberwareRating", "uiCyberwareMarkup", "uiCyberwareDiscount", "uiCyberwareBlackMarketDiscount");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.magic_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string magicName = ReadDialogValue(dialog, "uiMagicName", "Spell or Power");
            PublishRulesetAwareDialogNotice(context, $"Spell/power '{magicName}' added.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.magic_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string magicName = ReadDialogValue(dialog, "uiMagicName", "Spell or Power");
            PublishRulesetAwareDialogAddMore(context, dialog, $"Spell/power '{magicName}' added. Dialog remains open for another selection.", "uiMagicLevel");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.spell_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string spellName = ReadDialogValue(dialog, "uiSpellName", "Stunbolt");
            PublishRulesetAwareDialogNotice(context, $"Spell '{spellName}' added.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.spell_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string spellName = ReadDialogValue(dialog, "uiSpellName", "Stunbolt");
            PublishRulesetAwareDialogAddMore(context, dialog, $"Spell '{spellName}' added. Dialog remains open for another spell.", "uiSpellExtendedOnly");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.magic_delete", StringComparison.Ordinal) && string.Equals(actionId, "delete", StringComparison.Ordinal))
        {
            PublishRulesetAwareDialogNotice(context, "Spell/power deleted.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.skill_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string skillName = ReadDialogValue(dialog, "uiSkillName", "Perception");
            PublishRulesetAwareDialogNotice(context, $"Skill '{skillName}' added.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.skill_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string skillName = ReadDialogValue(dialog, "uiSkillName", "Perception");
            PublishRulesetAwareDialogAddMore(context, dialog, $"Skill '{skillName}' added. Dialog remains open for another skill.", "uiSkillRating");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.skill_specialize", StringComparison.Ordinal) && string.Equals(actionId, "apply", StringComparison.Ordinal))
        {
            string specialization = ReadDialogValue(dialog, "uiSkillSpec", "Visual");
            PublishRulesetAwareDialogNotice(context, $"Skill specialization set to '{specialization}'.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.skill_remove", StringComparison.Ordinal) && string.Equals(actionId, "delete", StringComparison.Ordinal))
        {
            PublishRulesetAwareDialogNotice(context, "Skill removed.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.combat_add_weapon", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string weaponName = ReadDialogValue(dialog, "uiWeaponName", "Colt M23");
            PublishRulesetAwareDialogNotice(context, $"Weapon '{weaponName}' added.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.combat_add_weapon", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string weaponName = ReadDialogValue(dialog, "uiWeaponName", "Colt M23");
            PublishRulesetAwareDialogAddMore(context, dialog, $"Weapon '{weaponName}' added. Dialog remains open for another weapon.", "uiWeaponMarkup", "uiWeaponFreeItem", "uiWeaponBlackMarketDiscount");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.combat_add_armor", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string armorName = ReadDialogValue(dialog, "uiArmorName", "Armor Jacket");
            PublishRulesetAwareDialogNotice(context, $"Armor '{armorName}' added.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.combat_add_armor", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string armorName = ReadDialogValue(dialog, "uiArmorName", "Armor Jacket");
            PublishRulesetAwareDialogAddMore(context, dialog, $"Armor '{armorName}' added. Dialog remains open for another armor item.", "uiArmorMarkup", "uiArmorFreeItem");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.contact_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string contactName = ReadDialogValue(dialog, "uiContactName", "Contact Name");
            PublishRulesetAwareDialogNotice(context, $"Contact '{contactName}' added.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.contact_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string contactName = ReadDialogValue(dialog, "uiContactName", "Contact Name");
            PublishRulesetAwareDialogAddMore(context, dialog, $"Contact '{contactName}' added. Dialog remains open for another contact.", "uiContactName", "uiContactConnection", "uiContactLoyalty");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.matrix_program_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string programName = ReadDialogValue(dialog, "uiMatrixProgramName", "Armor");
            PublishRulesetAwareDialogNotice(context, $"Program '{programName}' added.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.matrix_program_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string programName = ReadDialogValue(dialog, "uiMatrixProgramName", "Armor");
            PublishRulesetAwareDialogAddMore(context, dialog, $"Program '{programName}' added. Dialog remains open for another matrix entry.", "uiMatrixProgramShowDongles");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.vehicle_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string vehicleName = ReadDialogValue(dialog, "uiVehicleName", "Hyundai Shin-Hyung");
            PublishRulesetAwareDialogNotice(context, $"Vehicle '{vehicleName}' added.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.vehicle_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string vehicleName = ReadDialogValue(dialog, "uiVehicleName", "Hyundai Shin-Hyung");
            PublishRulesetAwareDialogAddMore(context, dialog, $"Vehicle '{vehicleName}' added. Dialog remains open for another entry.", "uiVehicleShowDrones");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.quality_add", StringComparison.Ordinal) && string.Equals(actionId, "add", StringComparison.Ordinal))
        {
            string qualityName = ReadDialogValue(dialog, "uiQualityName", "First Impression");
            PublishRulesetAwareDialogNotice(context, $"Quality '{qualityName}' added.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.quality_add", StringComparison.Ordinal) && string.Equals(actionId, "add_more", StringComparison.Ordinal))
        {
            string qualityName = ReadDialogValue(dialog, "uiQualityName", "First Impression");
            PublishRulesetAwareDialogAddMore(context, dialog, $"Quality '{qualityName}' added. Dialog remains open for another quality.", "uiQualityMetagenicOnly");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.contact_edit", StringComparison.Ordinal) && string.Equals(actionId, "apply", StringComparison.Ordinal))
        {
            string contactName = ReadDialogValue(dialog, "uiContactEditName", "Selected Contact");
            PublishRulesetAwareDialogNotice(context, $"Contact renamed to '{contactName}'.");
            return;
        }

        if (string.Equals(dialog.Id, "dialog.ui.contact_remove", StringComparison.Ordinal) && string.Equals(actionId, "delete", StringComparison.Ordinal))
        {
            PublishRulesetAwareDialogNotice(context, "Contact removed.");
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
        string notes = DesktopDialogFieldValueParser.GetValue(dialog, "characterNotes") ?? context.State.Preferences.CharacterNotes;

        context.Publish(context.State with
        {
            ActiveDialog = null,
            Error = null,
            Build = context.State.Build is null ? null : context.State.Build with { BuildMethod = priority },
            Preferences = context.State.Preferences with
            {
                CharacterPriority = priority,
                KarmaNuyenRatio = karmaNuyenRatio,
                HouseRulesEnabled = houseRules,
                CharacterNotes = notes
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
        string expression = DesktopDialogFieldValueParser.GetValue(dialog, "diceExpression") ?? "1d6";
        RulesetCapabilityInvocationResult result = await _engineEvaluator.InvokeAsync(
            new RulesetCapabilityInvocationRequest(
                CapabilityId: "ui.dice_roll",
                InvocationKind: RulesetCapabilityInvocationKinds.Rule,
                Arguments:
                [
                    new RulesetCapabilityArgument(
                        Name: "expression",
                        Value: new RulesetCapabilityValue(
                            Kind: RulesetCapabilityValueKinds.String,
                            StringValue: expression))
                ]),
            ct);

        if (!result.Success)
        {
            string error = result.Diagnostics.Count is 0
                ? "Dice evaluation failed."
                : result.Diagnostics[0].Message;
            context.Publish(context.State with { Error = error });
            return;
        }

        string? rawSummary = FormatEvaluationResult(result.Output);
        string summary = string.IsNullOrWhiteSpace(rawSummary) ? $"{expression} result" : $"{expression}: {rawSummary}";
        List<DesktopDialogField> fields = dialog.Fields
            .Where(field => !string.Equals(field.Id, "diceResult", StringComparison.Ordinal))
            .ToList();
        fields.Add(new DesktopDialogField(
            Id: "diceResult",
            Label: "Last Result",
            Value: summary,
            Placeholder: summary,
            IsMultiline: false,
            IsReadOnly: true));

        context.Publish(context.State with
        {
            Error = null,
            Notice = summary,
            ActiveDialog = dialog with
            {
                Message = "Dice expression evaluated by active ruleset.",
                Fields = fields
            }
        });
    }

    private static void DeriveInitiativePreview(
        DesktopDialogState dialog,
        DialogCoordinationContext context)
    {
        string preview = BuildInitiativePreview(dialog);
        string threshold = DesktopDialogFieldValueParser.GetValue(dialog, "diceThreshold") ?? "0";
        List<DesktopDialogField> fields = dialog.Fields
            .Where(field => !string.Equals(field.Id, "initiativePreview", StringComparison.Ordinal))
            .ToList();
        fields.Add(new DesktopDialogField(
            Id: "initiativePreview",
            Label: "Initiative Preview",
            Value: preview,
            Placeholder: preview,
            IsMultiline: false,
            IsReadOnly: true));

        context.Publish(context.State with
        {
            Error = null,
            Notice = $"Initiative preview refreshed (threshold {threshold}).",
            ActiveDialog = dialog with
            {
                Message = "Ruleset-backed initiative preview updated without closing the utility.",
                Fields = fields
            }
        });
    }

    private static string BuildInitiativePreview(DesktopDialogState dialog)
    {
        int initiativeBase = DesktopDialogFieldValueParser.ParseInt(dialog, "diceInitiativeBase", 10);
        int initiativeDice = DesktopDialogFieldValueParser.ParseInt(dialog, "diceInitiativeDice", 1);
        int woundModifier = DesktopDialogFieldValueParser.ParseInt(dialog, "diceWoundModifier", 0);
        int currentPass = DesktopDialogFieldValueParser.ParseInt(dialog, "diceCurrentPass", 1);

        int sanitizedDiceCount = Math.Max(0, initiativeDice);
        int sanitizedPass = Math.Max(1, currentPass);
        int modifiedBase = initiativeBase + woundModifier;
        int min = modifiedBase + sanitizedDiceCount;
        int max = modifiedBase + (sanitizedDiceCount * 6);
        decimal average = modifiedBase + (sanitizedDiceCount * 3.5m);

        return sanitizedDiceCount == 0
            ? $"{modifiedBase} flat · pass {sanitizedPass}"
            : $"{modifiedBase} + {sanitizedDiceCount}d6 · pass {sanitizedPass} · range {min}-{max} · avg {average:0.0}";
    }

    private static string? FormatEvaluationResult(RulesetCapabilityValue? value)
    {
        if (value is null)
            return null;

        return value.Kind switch
        {
            RulesetCapabilityValueKinds.String => value.StringValue,
            RulesetCapabilityValueKinds.Boolean => value.BooleanValue?.ToString(CultureInfo.InvariantCulture),
            RulesetCapabilityValueKinds.Integer => value.IntegerValue?.ToString(CultureInfo.InvariantCulture),
            RulesetCapabilityValueKinds.Number => value.NumberValue?.ToString(CultureInfo.InvariantCulture),
            RulesetCapabilityValueKinds.Decimal => value.DecimalValue?.ToString(CultureInfo.InvariantCulture),
            RulesetCapabilityValueKinds.List => FormatList(value.Items),
            RulesetCapabilityValueKinds.Object => FormatObject(value.Properties),
            _ => value.StringValue
        };
    }

    private static string FormatList(IReadOnlyList<RulesetCapabilityValue>? values)
    {
        if (values is null || values.Count is 0)
            return "[]";

        StringBuilder builder = new();
        builder.Append('[');
        for (int index = 0; index < values.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(FormatEvaluationResult(values[index]) ?? "null");
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static string FormatObject(IReadOnlyDictionary<string, RulesetCapabilityValue>? values)
    {
        if (values is null || values.Count is 0)
            return "{}";

        StringBuilder builder = new();
        builder.Append('{');
        int index = 0;
        foreach (KeyValuePair<string, RulesetCapabilityValue> item in values)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(item.Key);
            builder.Append(": ");
            builder.Append(FormatEvaluationResult(item.Value) ?? "null");
            index++;
        }

        builder.Append('}');
        return builder.ToString();
    }

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

    private static void OpenCharacterSettingsFromMasterIndex(DialogCoordinationContext context)
    {
        DesktopDialogFactory dialogFactory = new();
        CharacterOverviewState state = context.GetState();
        DesktopDialogState characterSettingsDialog = dialogFactory.CreateCommandDialog(
            "character_settings",
            state.Profile,
            state.Preferences,
            state.ActiveSectionJson,
            state.WorkspaceId,
            ResolveContextRulesetId(state),
            openWorkspaces: state.OpenWorkspaces);

        context.Publish(state with
        {
            ActiveDialog = characterSettingsDialog,
            Error = null,
            Notice = "Character Settings opened from Master Index."
        });
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

    private sealed record MasterIndexCoordinatorSnapshot(
        string SettingsLanePosture,
        List<MasterIndexCoordinatorFileSnapshot> Files,
        List<MasterIndexCoordinatorSourcebookSnapshot> Sourcebooks);

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
