namespace Chummer.Presentation.Overview;

public sealed partial class DesktopDialogFactory
{
    internal static DesktopDialogState BuildCharacterSettingsDialog(
        DesktopPreferenceState preferences,
        string? requestedProfileId = null,
        string? requestedSectionId = null,
        string? draftXml = null,
        string? requestedProfileName = null)
    {
        Chummer5CharacterSettingsCatalog catalog = Chummer5CharacterSettingsProfiles.ParseCatalog(
            preferences.CharacterSettingsCatalogJson);
        Chummer5CharacterSettingsProfile profile = Chummer5CharacterSettingsProfiles.ActiveProfile(
            catalog,
            requestedProfileId);
        string sectionId = ResolveSectionId(requestedSectionId);
        string effectiveDraftXml = string.IsNullOrWhiteSpace(draftXml) ? profile.Xml : draftXml;
        string profileName = string.IsNullOrWhiteSpace(requestedProfileName)
            ? profile.Name
            : requestedProfileName.Trim();

        List<DesktopDialogField> fields =
        [
            new DesktopDialogField(
                Chummer5CharacterSettingsProfiles.ProfileFieldId,
                "Settings profile",
                profile.Id,
                profile.Id,
                InputType: "select",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                Options: catalog.Profiles
                    .Select(item => new DesktopDialogFieldOption(item.Id, item.Name))
                    .ToArray()),
            new DesktopDialogField(
                Chummer5CharacterSettingsProfiles.ProfileNameFieldId,
                "Profile name",
                profileName,
                profile.Name,
                LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField(
                Chummer5CharacterSettingsProfiles.SectionFieldId,
                "Settings section",
                sectionId,
                "build",
                InputType: "select",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Full,
                Options: Chummer5CharacterSettingsRuntimeContractGenerated.Sections
                    .Select(section => new DesktopDialogFieldOption(section.Id, section.Label))
                    .ToArray()),
            new DesktopDialogField(
                Chummer5CharacterSettingsProfiles.LoadedProfileFieldId,
                "Loaded settings profile",
                profile.Id,
                profile.Id,
                IsReadOnly: true,
                LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField(
                Chummer5CharacterSettingsProfiles.DraftXmlFieldId,
                "Settings profile draft",
                effectiveDraftXml,
                string.Empty,
                IsMultiline: true,
                IsReadOnly: true,
                LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden)
        ];

        int visibleIndex = 0;
        foreach (Chummer5CharacterSettingsFieldDefinition definition in Chummer5CharacterSettingsRuntimeContractGenerated.Fields
            .Where(field => string.Equals(field.SectionId, sectionId, StringComparison.Ordinal)))
        {
            IReadOnlyList<DesktopDialogFieldOption>? options = definition.Options.Count == 0
                ? null
                : definition.Options
                    .Select(value => new DesktopDialogFieldOption(value, FormatOptionLabel(value)))
                    .ToArray();
            string layoutSlot = definition.IsMultiline
                ? DesktopDialogFieldLayoutSlots.Full
                : visibleIndex++ % 2 == 0
                    ? DesktopDialogFieldLayoutSlots.Left
                    : DesktopDialogFieldLayoutSlots.Right;
            fields.Add(new DesktopDialogField(
                Chummer5CharacterSettingsProfiles.FieldId(definition.LegacyControl),
                definition.Label,
                Chummer5CharacterSettingsProfiles.ReadFieldValue(effectiveDraftXml, definition),
                Chummer5CharacterSettingsProfiles.BuildPlaceholder(definition),
                IsMultiline: definition.IsMultiline,
                InputType: definition.InputType,
                LayoutSlot: layoutSlot,
                Options: options));
        }

        return new DesktopDialogState(
            Chummer5CharacterSettingsProfiles.DialogId,
            "Character Settings",
            "Edit the complete Chummer5-compatible settings profile. Switch sections freely; Save commits the current profile.",
            fields,
            [
                new DesktopDialogAction(Chummer5CharacterSettingsProfiles.SaveActionId, "Save"),
                new DesktopDialogAction(Chummer5CharacterSettingsProfiles.SaveAndCloseActionId, "Save & Close", true),
                new DesktopDialogAction(Chummer5CharacterSettingsProfiles.SaveAsActionId, "Save As"),
                new DesktopDialogAction(Chummer5CharacterSettingsProfiles.RenameActionId, "Rename"),
                new DesktopDialogAction(Chummer5CharacterSettingsProfiles.DeleteActionId, "Delete"),
                new DesktopDialogAction(Chummer5CharacterSettingsProfiles.RestoreDefaultsActionId, "Restore Defaults"),
                new DesktopDialogAction("cancel", "Cancel")
            ]);
    }

    internal static DesktopDialogState RebuildCharacterSettingsDialog(
        DesktopDialogState dialog,
        DesktopPreferenceState fallback)
    {
        Chummer5CharacterSettingsCatalog catalog = Chummer5CharacterSettingsProfiles.ParseCatalog(
            fallback.CharacterSettingsCatalogJson);
        string loadedProfileId = DesktopDialogFieldValueParser.GetValue(
            dialog,
            Chummer5CharacterSettingsProfiles.LoadedProfileFieldId) ?? catalog.ActiveProfileId;
        string requestedProfileId = DesktopDialogFieldValueParser.GetValue(
            dialog,
            Chummer5CharacterSettingsProfiles.ProfileFieldId) ?? loadedProfileId;
        string requestedSection = DesktopDialogFieldValueParser.GetValue(
            dialog,
            Chummer5CharacterSettingsProfiles.SectionFieldId) ?? "build";
        string profileName = DesktopDialogFieldValueParser.GetValue(
            dialog,
            Chummer5CharacterSettingsProfiles.ProfileNameFieldId) ?? string.Empty;

        if (!string.Equals(loadedProfileId, requestedProfileId, StringComparison.Ordinal))
        {
            Chummer5CharacterSettingsProfile requested = Chummer5CharacterSettingsProfiles.ActiveProfile(
                catalog,
                requestedProfileId);
            return BuildCharacterSettingsDialog(
                fallback,
                requested.Id,
                requestedSection,
                requested.Xml,
                requested.Name);
        }

        string draftXml = DesktopDialogFieldValueParser.GetValue(
            dialog,
            Chummer5CharacterSettingsProfiles.DraftXmlFieldId)
            ?? Chummer5CharacterSettingsProfiles.ActiveProfile(catalog, loadedProfileId).Xml;
        if (Chummer5CharacterSettingsProfiles.TryApplyVisibleFields(
            dialog,
            draftXml,
            out string updatedDraftXml,
            out _))
        {
            draftXml = updatedDraftXml;
        }
        return BuildCharacterSettingsDialog(
            fallback,
            loadedProfileId,
            requestedSection,
            draftXml,
            profileName);
    }

    private static string ResolveSectionId(string? requested)
    {
        string normalized = string.IsNullOrWhiteSpace(requested) ? "build" : requested.Trim();
        return Chummer5CharacterSettingsRuntimeContractGenerated.Sections.Any(
            section => string.Equals(section.Id, normalized, StringComparison.Ordinal))
            ? normalized
            : "build";
    }

    private static string FormatOptionLabel(string value)
        => value switch
        {
            "SumtoTen" => "Sum to Ten",
            "LifeModule" => "Life Modules",
            "4<torso,skull" => "4 limbs (2 arms, 2 legs)",
            "5<torso" => "5 limbs (include skull)",
            "5<skull" => "5 limbs (include torso)",
            _ => value
        };
}
