using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace Chummer.Presentation.Overview;

internal sealed record Chummer5CharacterSettingsSectionDefinition(
    string Id,
    string Label);

internal sealed record Chummer5CharacterSettingsFieldDefinition(
    string LegacyControl,
    string Label,
    string SectionId,
    string InputType,
    bool IsMultiline,
    IReadOnlyList<string> PersistencePaths,
    IReadOnlyList<string> Options);

internal sealed record Chummer5CharacterSettingsProfile(
    string Id,
    string Name,
    string Xml);

internal sealed record Chummer5CharacterSettingsCatalog(
    string ActiveProfileId,
    IReadOnlyList<Chummer5CharacterSettingsProfile> Profiles);

internal static class Chummer5CharacterSettingsProfiles
{
    internal const string DialogId = "dialog.character_settings";
    internal const string ProfileFieldId = "characterSettingsProfile";
    internal const string ProfileNameFieldId = "characterSettingsProfileName";
    internal const string SectionFieldId = "characterSettingsSection";
    internal const string LoadedProfileFieldId = "characterSettingsLoadedProfile";
    internal const string DraftXmlFieldId = "characterSettingsDraftXml";
    internal const string EditedFieldIdsFieldId = "characterSettingsEditedFieldIds";
    internal const string FieldPrefix = "characterSettingsControl";
    internal const string SaveActionId = "save";
    internal const string SaveAndCloseActionId = "save_and_close";
    internal const string SaveAsActionId = "save_as";
    internal const string RenameActionId = "rename";
    internal const string DeleteActionId = "delete";
    internal const string RestoreDefaultsActionId = "restore_defaults";

    private const string StandardProfileId = "223a11ff-80e0-428b-89a9-6ef1c243b8b6";
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static string FieldId(string legacyControl)
        => $"{FieldPrefix}-{legacyControl}";

    internal static Chummer5CharacterSettingsCatalog ParseCatalog(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                Chummer5CharacterSettingsCatalog? parsed = JsonSerializer.Deserialize<Chummer5CharacterSettingsCatalog>(json, s_jsonOptions);
                if (parsed is not null)
                    return NormalizeCatalog(parsed);
            }
            catch (JsonException)
            {
                // Fall through to the deterministic Standard profile.
            }
        }

        Chummer5CharacterSettingsProfile standard = CreateStandardProfile();
        return new Chummer5CharacterSettingsCatalog(standard.Id, [standard]);
    }

    internal static string SerializeCatalog(Chummer5CharacterSettingsCatalog catalog)
        => JsonSerializer.Serialize(NormalizeCatalog(catalog), s_jsonOptions);

    internal static Chummer5CharacterSettingsProfile ActiveProfile(
        Chummer5CharacterSettingsCatalog catalog,
        string? requestedProfileId = null)
    {
        string profileId = string.IsNullOrWhiteSpace(requestedProfileId)
            ? catalog.ActiveProfileId
            : requestedProfileId.Trim();
        return catalog.Profiles.FirstOrDefault(profile => string.Equals(profile.Id, profileId, StringComparison.Ordinal))
            ?? catalog.Profiles[0];
    }

    internal static bool TryApplyVisibleFields(
        DesktopDialogState dialog,
        string draftXml,
        out string updatedXml,
        out string? error)
    {
        updatedXml = draftXml;
        if (!TryParseSettingsXmlForMutation(
            draftXml,
            out XDocument document,
            out XElement settings,
            out error))
            return false;

        if (!TryReadEditedFieldIds(dialog, out IReadOnlyList<string> editedFieldIds, out error))
            return false;

        foreach (string fieldId in editedFieldIds)
        {
            Chummer5CharacterSettingsFieldDefinition? definition = Chummer5CharacterSettingsRuntimeContractGenerated.Fields
                .SingleOrDefault(candidate => string.Equals(FieldId(candidate.LegacyControl), fieldId, StringComparison.Ordinal));
            if (definition is null)
            {
                error = $"Character settings field '{fieldId}' is not supported.";
                return false;
            }

            DesktopDialogField[] submittedFields = dialog.Fields
                .Where(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal))
                .ToArray();
            if (submittedFields.Length != 1)
            {
                error = $"Character settings field '{fieldId}' must be submitted exactly once.";
                return false;
            }

            string value = submittedFields[0].Value;
            if (!TryApplyField(settings, definition, value, out error))
                return false;
        }

        if (editedFieldIds.Count == 0)
        {
            error = null;
            return true;
        }

        string documentXml = document.ToString(SaveOptions.DisableFormatting);
        updatedXml = document.Declaration is null
            ? documentXml
            : $"{document.Declaration}{documentXml}";
        error = null;
        return true;
    }

    internal static bool IsValueFieldId(string fieldId)
        => fieldId.StartsWith($"{FieldPrefix}-", StringComparison.Ordinal)
            && Chummer5CharacterSettingsRuntimeContractGenerated.Fields.Any(
                definition => string.Equals(FieldId(definition.LegacyControl), fieldId, StringComparison.Ordinal));

    internal static string RecordEditedFieldId(string? currentValue, string fieldId)
    {
        HashSet<string> edited = new(
            SplitEditedFieldIds(currentValue),
            StringComparer.Ordinal);
        edited.Add(fieldId);
        return string.Join('\n', edited.OrderBy(value => value, StringComparer.Ordinal));
    }

    internal static string ReadFieldValue(
        string xml,
        Chummer5CharacterSettingsFieldDefinition definition)
    {
        XElement settings = ParseSettingsXml(xml, ActiveProfile(ParseCatalog(null)));
        string control = definition.LegacyControl;
        if (string.Equals(control, "treSourcebook", StringComparison.Ordinal)
            || string.Equals(control, "chkGrade", StringComparison.Ordinal))
        {
            return string.Join(Environment.NewLine, ElementsAtPath(settings, definition.PersistencePaths[0])
                .Select(element => element.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        if (string.Equals(control, "treCustomDataDirectories", StringComparison.Ordinal))
            return ReadCustomDataDirectories(settings);

        if (control.StartsWith("chkRedlinerLimbs", StringComparison.Ordinal))
        {
            string limb = RedlinerLimb(control);
            return ElementsAtPath(settings, "settings/redlinerexclusion/limb")
                .Any(element => string.Equals(element.Value, limb, StringComparison.OrdinalIgnoreCase))
                ? "true"
                : "false";
        }

        if (string.Equals(control, "cboLimbCount", StringComparison.Ordinal))
        {
            string count = ReadSingle(settings, "settings/limbcount", "6");
            string excluded = ReadSingle(settings, "settings/excludelimbslot", string.Empty);
            return string.IsNullOrWhiteSpace(excluded) ? count : $"{count}<{excluded}";
        }

        if (string.Equals(control, "nudNuyenDecimalsMinimum", StringComparison.Ordinal))
            return DecimalPlaces(ReadSingle(settings, "settings/nuyenformat", "#,0.##")).Minimum.ToString(CultureInfo.InvariantCulture);
        if (string.Equals(control, "nudNuyenDecimalsMaximum", StringComparison.Ordinal))
            return DecimalPlaces(ReadSingle(settings, "settings/nuyenformat", "#,0.##")).Maximum.ToString(CultureInfo.InvariantCulture);
        if (string.Equals(control, "nudEssenceDecimals", StringComparison.Ordinal))
            return DecimalPlaces(ReadSingle(settings, "settings/essenceformat", "#,0.00")).Maximum.ToString(CultureInfo.InvariantCulture);
        if (string.Equals(control, "nudWeightDecimals", StringComparison.Ordinal))
            return DecimalPlaces(ReadSingle(settings, "settings/weightformat", "#,0.###")).Maximum.ToString(CultureInfo.InvariantCulture);

        string fallback = definition.InputType switch
        {
            "checkbox" => "false",
            "number" => "0",
            _ => string.Empty
        };
        string value = ReadSingle(settings, definition.PersistencePaths[0], fallback);
        return string.Equals(definition.InputType, "checkbox", StringComparison.Ordinal)
            ? bool.TryParse(value, out bool parsed) && parsed ? "true" : "false"
            : value;
    }

    internal static Chummer5CharacterSettingsCatalog Save(
        Chummer5CharacterSettingsCatalog catalog,
        string profileId,
        string profileName,
        string xml)
    {
        Chummer5CharacterSettingsProfile current = ActiveProfile(catalog, profileId);
        string normalizedName = NormalizeName(profileName, current.Name);
        Chummer5CharacterSettingsProfile updated = NormalizeProfile(
            new Chummer5CharacterSettingsProfile(current.Id, normalizedName, xml));
        Chummer5CharacterSettingsProfile[] profiles = catalog.Profiles
            .Select(profile => string.Equals(profile.Id, current.Id, StringComparison.Ordinal) ? updated : profile)
            .ToArray();
        return NormalizeCatalog(new Chummer5CharacterSettingsCatalog(updated.Id, profiles));
    }

    internal static Chummer5CharacterSettingsCatalog SaveAs(
        Chummer5CharacterSettingsCatalog catalog,
        string profileName,
        string xml)
    {
        string normalizedName = UniqueProfileName(catalog, NormalizeName(profileName, "Custom settings"));
        string id = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        Chummer5CharacterSettingsProfile profile = NormalizeProfile(
            new Chummer5CharacterSettingsProfile(id, normalizedName, xml));
        return NormalizeCatalog(new Chummer5CharacterSettingsCatalog(
            id,
            [.. catalog.Profiles, profile]));
    }

    internal static Chummer5CharacterSettingsCatalog Rename(
        Chummer5CharacterSettingsCatalog catalog,
        string profileId,
        string profileName,
        string draftXml)
        => Save(catalog, profileId, profileName, draftXml);

    internal static Chummer5CharacterSettingsCatalog Delete(
        Chummer5CharacterSettingsCatalog catalog,
        string profileId)
    {
        Chummer5CharacterSettingsProfile current = ActiveProfile(catalog, profileId);
        Chummer5CharacterSettingsProfile[] remaining = catalog.Profiles
            .Where(profile => !string.Equals(profile.Id, current.Id, StringComparison.Ordinal))
            .ToArray();
        if (remaining.Length == 0)
        {
            Chummer5CharacterSettingsProfile standard = CreateStandardProfile();
            remaining = [standard];
        }
        return NormalizeCatalog(new Chummer5CharacterSettingsCatalog(remaining[0].Id, remaining));
    }

    internal static string RestoreDefaults(string profileId, string profileName)
    {
        Chummer5CharacterSettingsProfile standard = CreateStandardProfile();
        XElement settings = XElement.Parse(standard.Xml, LoadOptions.PreserveWhitespace);
        SetSingle(settings, "settings/id", profileId);
        SetSingle(settings, "settings/name", NormalizeName(profileName, standard.Name));
        return settings.ToString(SaveOptions.DisableFormatting);
    }

    internal static string BuildPlaceholder(Chummer5CharacterSettingsFieldDefinition definition)
        => definition.LegacyControl switch
        {
            "treSourcebook" => "One sourcebook code per line",
            "treCustomDataDirectories" => "[x] directory-key or [ ] directory-key, in load order",
            "chkGrade" => "One banned ware grade per line",
            _ => ReadDefaultValue(definition)
        };

    internal static string ReadBuildMethod(string xml, string fallback)
    {
        XElement settings = ParseSettingsXml(xml, ActiveProfile(ParseCatalog(null)));
        string value = ReadSingle(settings, "settings/buildmethod", fallback);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static Chummer5CharacterSettingsCatalog NormalizeCatalog(Chummer5CharacterSettingsCatalog catalog)
    {
        List<Chummer5CharacterSettingsProfile> profiles = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (Chummer5CharacterSettingsProfile candidate in catalog.Profiles ?? [])
        {
            Chummer5CharacterSettingsProfile profile = NormalizeProfile(candidate);
            if (ids.Add(profile.Id))
                profiles.Add(profile);
        }
        if (profiles.Count == 0)
            profiles.Add(CreateStandardProfile());
        string active = profiles.Any(profile => string.Equals(profile.Id, catalog.ActiveProfileId, StringComparison.Ordinal))
            ? catalog.ActiveProfileId
            : profiles[0].Id;
        return new Chummer5CharacterSettingsCatalog(active, profiles);
    }

    private static Chummer5CharacterSettingsProfile NormalizeProfile(Chummer5CharacterSettingsProfile profile)
    {
        string id = string.IsNullOrWhiteSpace(profile.Id)
            ? Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture)
            : profile.Id.Trim();
        string name = NormalizeName(profile.Name, "Custom settings");
        return new Chummer5CharacterSettingsProfile(id, name, profile.Xml ?? string.Empty);
    }

    private static Chummer5CharacterSettingsProfile CreateStandardProfile()
    {
        XElement settings = new("settings");
        foreach ((string path, IReadOnlyList<string> values) in Chummer5CharacterSettingsRuntimeContractGenerated.BuiltInStandardValues)
        {
            if (values.Count == 0 || path.Contains("customdatadirectoryname/", StringComparison.Ordinal))
                continue;
            SetPathValues(settings, path, values);
        }
        SetSingle(settings, "settings/id", StandardProfileId);
        SetSingle(settings, "settings/name", "Standard");
        SetSingle(settings, "settings/gameplayoptionname", "Standard");
        return new Chummer5CharacterSettingsProfile(
            StandardProfileId,
            "Standard",
            settings.ToString(SaveOptions.DisableFormatting));
    }

    private static bool TryApplyField(
        XElement settings,
        Chummer5CharacterSettingsFieldDefinition definition,
        string value,
        out string? error)
    {
        string control = definition.LegacyControl;
        if (string.Equals(control, "treSourcebook", StringComparison.Ordinal)
            || string.Equals(control, "chkGrade", StringComparison.Ordinal))
        {
            return TrySetPathValues(settings, definition.PersistencePaths[0], SplitCollection(value), out error);
        }
        if (string.Equals(control, "treCustomDataDirectories", StringComparison.Ordinal))
            return TryWriteCustomDataDirectories(settings, value, out error);
        if (control.StartsWith("chkRedlinerLimbs", StringComparison.Ordinal))
            return TrySetMembership(
                settings,
                "settings/redlinerexclusion/limb",
                RedlinerLimb(control),
                ParseBool(value),
                out error);
        if (string.Equals(control, "cboLimbCount", StringComparison.Ordinal))
        {
            string[] parts = value.Split('<', 2, StringSplitOptions.TrimEntries);
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) || count <= 0)
            {
                error = "Cyberlimb configuration must start with a positive limb count.";
                return false;
            }
            return TrySetSingle(
                    settings,
                    "settings/limbcount",
                    count.ToString(CultureInfo.InvariantCulture),
                    out error)
                && TrySetSingle(
                    settings,
                    "settings/excludelimbslot",
                    parts.Length > 1 ? parts[1] : string.Empty,
                    out error);
        }
        if (control is "nudNuyenDecimalsMinimum" or "nudNuyenDecimalsMaximum" or "nudEssenceDecimals" or "nudWeightDecimals")
            return TryApplyDecimalFormat(settings, control, value, out error);
        if (string.Equals(definition.InputType, "checkbox", StringComparison.Ordinal))
            return TrySetSingle(
                settings,
                definition.PersistencePaths[0],
                ParseBool(value) ? "True" : "False",
                out error);
        if (string.Equals(definition.InputType, "number", StringComparison.Ordinal))
        {
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
            {
                error = $"{definition.Label} must be a number.";
                return false;
            }
            return TrySetSingle(
                settings,
                definition.PersistencePaths[0],
                number.ToString(CultureInfo.InvariantCulture),
                out error);
        }
        if (definition.Options.Count > 0
            && !definition.Options.Contains(value, StringComparer.Ordinal))
        {
            error = $"{definition.Label} has an unsupported value.";
            return false;
        }
        return TrySetSingle(settings, definition.PersistencePaths[0], value.Trim(), out error);
    }

    private static bool TryApplyDecimalFormat(XElement settings, string control, string value, out string? error)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int decimals)
            || decimals < 0
            || decimals > 10)
        {
            error = "Decimal places must be between 0 and 10.";
            return false;
        }
        if (string.Equals(control, "nudNuyenDecimalsMinimum", StringComparison.Ordinal)
            || string.Equals(control, "nudNuyenDecimalsMaximum", StringComparison.Ordinal))
        {
            (int minimum, int maximum) = DecimalPlaces(ReadSingle(settings, "settings/nuyenformat", "#,0.##"));
            if (string.Equals(control, "nudNuyenDecimalsMinimum", StringComparison.Ordinal))
                minimum = decimals;
            else
                maximum = decimals;
            maximum = Math.Max(maximum, minimum);
            minimum = Math.Min(minimum, maximum);
            return TrySetSingle(settings, "settings/nuyenformat", DecimalFormat(minimum, maximum), out error);
        }
        string path = string.Equals(control, "nudEssenceDecimals", StringComparison.Ordinal)
            ? "settings/essenceformat"
            : "settings/weightformat";
        return TrySetSingle(settings, path, DecimalFormat(decimals, decimals), out error);
    }

    private static string ReadCustomDataDirectories(XElement settings)
    {
        XElement? parent = ElementAtPath(settings, "settings/customdatadirectorynames");
        if (parent is null)
            return string.Empty;
        return string.Join(Environment.NewLine, parent.Elements("customdatadirectoryname")
            .OrderBy(element => int.TryParse(element.Element("order")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int order) ? order : int.MaxValue)
            .Select(element => $"[{(ParseBool(element.Element("enabled")?.Value) ? 'x' : ' ')}] {element.Element("directoryname")?.Value ?? string.Empty}"));
    }

    private static bool TryWriteCustomDataDirectories(XElement settings, string value, out string? error)
    {
        if (!TryResolveElement(settings, "settings/customdatadirectorynames", out XElement parent, out error))
            return false;
        XElement[] entries = parent.Elements()
            .Where(element => string.Equals(element.Name.LocalName, "customdatadirectoryname", StringComparison.Ordinal))
            .ToArray();
        if (entries.Any(element => element.Name.Namespace != XNamespace.None))
        {
            error = "Character settings path 'settings/customdatadirectorynames/customdatadirectoryname' has a namespace mismatch.";
            return false;
        }
        foreach (XElement entry in entries)
            entry.Remove();
        int order = 0;
        foreach (string rawLine in value.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            bool enabled = true;
            if (line.StartsWith("[x]", StringComparison.OrdinalIgnoreCase))
                line = line[3..].Trim();
            else if (line.StartsWith("[ ]", StringComparison.Ordinal))
            {
                enabled = false;
                line = line[3..].Trim();
            }
            if (line.Length == 0)
                continue;
            order++;
            parent.Add(new XElement(
                "customdatadirectoryname",
                new XElement("directoryname", line),
                new XElement("order", order.ToString(CultureInfo.InvariantCulture)),
                new XElement("enabled", enabled ? "True" : "False")));
        }
        error = null;
        return true;
    }

    private static bool TryParseSettingsXmlForMutation(
        string xml,
        out XDocument document,
        out XElement settings,
        out string? error)
    {
        document = null!;
        settings = null!;
        if (string.IsNullOrWhiteSpace(xml))
        {
            error = "Character settings XML is required.";
            return false;
        }

        try
        {
            document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException exception)
        {
            error = $"Character settings XML is malformed: {exception.Message}";
            return false;
        }

        if (document.Root is not XElement root)
        {
            error = "Character settings XML must have an unnamespaced <settings> root.";
            document = null!;
            return false;
        }
        settings = root;

        if (settings.Name != XName.Get("settings"))
        {
            error = settings.Name.LocalName == "settings"
                ? "Character settings root has a namespace mismatch."
                : "Character settings XML must have an unnamespaced <settings> root.";
            settings = null!;
            document = null!;
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryReadEditedFieldIds(
        DesktopDialogState dialog,
        out IReadOnlyList<string> editedFieldIds,
        out string? error)
    {
        DesktopDialogField[] trackers = dialog.Fields
            .Where(field => string.Equals(field.Id, EditedFieldIdsFieldId, StringComparison.Ordinal))
            .ToArray();
        if (trackers.Length > 1)
        {
            editedFieldIds = [];
            error = "Character settings edited-field tracking is ambiguous.";
            return false;
        }

        string[] parsed = SplitEditedFieldIds(trackers.SingleOrDefault()?.Value);
        string? unsupported = parsed.FirstOrDefault(fieldId => !IsValueFieldId(fieldId));
        if (unsupported is not null)
        {
            editedFieldIds = [];
            error = $"Character settings field '{unsupported}' is not supported.";
            return false;
        }

        editedFieldIds = parsed.Distinct(StringComparer.Ordinal).ToArray();
        error = null;
        return true;
    }

    private static string[] SplitEditedFieldIds(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Replace("\r", string.Empty, StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static XElement ParseSettingsXml(string? xml, Chummer5CharacterSettingsProfile fallback)
    {
        if (!string.IsNullOrWhiteSpace(xml))
        {
            try
            {
                XElement parsed = XElement.Parse(xml, LoadOptions.PreserveWhitespace);
                if (string.Equals(parsed.Name.LocalName, "settings", StringComparison.Ordinal))
                    return new XElement(parsed);
                if (string.Equals(parsed.Name.LocalName, "setting", StringComparison.Ordinal))
                {
                    parsed.Name = "settings";
                    return new XElement(parsed);
                }
            }
            catch (System.Xml.XmlException)
            {
                // Use a deterministic minimal profile below.
            }
        }
        return new XElement(
            "settings",
            new XElement("id", fallback.Id),
            new XElement("name", fallback.Name),
            new XElement("gameplayoptionname", fallback.Name));
    }

    private static XElement EnsurePath(XElement settings, string path)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int index = string.Equals(segments[0], settings.Name.LocalName, StringComparison.Ordinal) ? 1 : 0;
        XElement current = settings;
        for (; index < segments.Length; index++)
        {
            XElement? next = current.Element(segments[index]);
            if (next is null)
            {
                next = new XElement(segments[index]);
                current.Add(next);
            }
            current = next;
        }
        return current;
    }

    private static XElement? ElementAtPath(XElement settings, string path)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int index = string.Equals(segments[0], settings.Name.LocalName, StringComparison.Ordinal) ? 1 : 0;
        XElement? current = settings;
        for (; index < segments.Length && current is not null; index++)
            current = current.Element(segments[index]);
        return current;
    }

    private static IEnumerable<XElement> ElementsAtPath(XElement settings, string path)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int start = string.Equals(segments[0], settings.Name.LocalName, StringComparison.Ordinal) ? 1 : 0;
        IEnumerable<XElement> current = [settings];
        for (int index = start; index < segments.Length; index++)
        {
            string segment = segments[index];
            current = current.SelectMany(element => element.Elements(segment));
        }
        return current;
    }

    private static string ReadSingle(XElement settings, string path, string fallback)
        => ElementsAtPath(settings, path).FirstOrDefault()?.Value ?? fallback;

    private static bool TrySetSingle(
        XElement settings,
        string path,
        string value,
        out string? error)
    {
        if (!TryResolveParent(settings, path, out XElement parent, out string leaf, out error))
            return false;

        XElement[] matching = parent.Elements()
            .Where(element => string.Equals(element.Name.LocalName, leaf, StringComparison.Ordinal))
            .ToArray();
        if (matching.Any(element => element.Name.Namespace != XNamespace.None))
        {
            error = $"Character settings path '{path}' has a namespace mismatch.";
            return false;
        }
        if (matching.Length > 1)
        {
            error = $"Character settings path '{path}' is ambiguous because it has duplicate targets.";
            return false;
        }

        XElement target;
        if (matching.Length == 0)
        {
            target = new XElement(leaf);
            parent.Add(target);
        }
        else
        {
            target = matching[0];
        }

        XNode[] content = target.Nodes().ToArray();
        if (content.Any(node => node is not XText) || content.OfType<XText>().Count() > 1)
        {
            error = $"Character settings path '{path}' has complex content that cannot be replaced safely.";
            return false;
        }

        XText? text = content.OfType<XText>().SingleOrDefault();
        if (text is null)
            target.Add(new XText(value));
        else
            text.Value = value;
        error = null;
        return true;
    }

    private static bool TrySetPathValues(
        XElement settings,
        string path,
        IEnumerable<string> values,
        out string? error)
    {
        if (!TryResolveParent(settings, path, out XElement parent, out string leaf, out error))
            return false;

        XElement[] matching = parent.Elements()
            .Where(element => string.Equals(element.Name.LocalName, leaf, StringComparison.Ordinal))
            .ToArray();
        if (matching.Any(element => element.Name.Namespace != XNamespace.None))
        {
            error = $"Character settings path '{path}' has a namespace mismatch.";
            return false;
        }

        foreach (XElement element in matching)
            element.Remove();
        foreach (string item in values.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal))
            parent.Add(new XElement(leaf, item.Trim()));
        error = null;
        return true;
    }

    private static bool TrySetMembership(
        XElement settings,
        string path,
        string value,
        bool enabled,
        out string? error)
    {
        if (!TryResolveParent(settings, path, out XElement parent, out string leaf, out error))
            return false;

        XElement[] matching = parent.Elements()
            .Where(element => string.Equals(element.Name.LocalName, leaf, StringComparison.Ordinal))
            .ToArray();
        if (matching.Any(element => element.Name.Namespace != XNamespace.None))
        {
            error = $"Character settings path '{path}' has a namespace mismatch.";
            return false;
        }

        XElement[] selected = matching
            .Where(element => string.Equals(element.Value, value, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (selected.Length > 1)
        {
            error = $"Character settings path '{path}' is ambiguous because value '{value}' has duplicate targets.";
            return false;
        }

        if (!enabled)
            selected.SingleOrDefault()?.Remove();
        else if (selected.Length == 0)
            parent.Add(new XElement(leaf, value));
        error = null;
        return true;
    }

    private static bool TryResolveElement(
        XElement settings,
        string path,
        out XElement element,
        out string? error)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int index = segments.Length > 0 && string.Equals(segments[0], settings.Name.LocalName, StringComparison.Ordinal)
            ? 1
            : 0;
        element = settings;
        for (; index < segments.Length; index++)
        {
            string segment = segments[index];
            XElement[] matching = element.Elements()
                .Where(child => string.Equals(child.Name.LocalName, segment, StringComparison.Ordinal))
                .ToArray();
            if (matching.Any(child => child.Name.Namespace != XNamespace.None))
            {
                error = $"Character settings path '{path}' has a namespace mismatch.";
                return false;
            }
            if (matching.Length > 1)
            {
                error = $"Character settings path '{path}' is ambiguous because segment '{segment}' is duplicated.";
                return false;
            }
            if (matching.Length == 0)
            {
                XElement child = new(segment);
                element.Add(child);
                element = child;
            }
            else
            {
                element = matching[0];
            }
        }

        error = null;
        return true;
    }

    private static bool TryResolveParent(
        XElement settings,
        string path,
        out XElement parent,
        out string leaf,
        out string? error)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            parent = settings;
            leaf = string.Empty;
            error = "Character settings persistence path is empty.";
            return false;
        }

        leaf = segments[^1];
        string parentPath = string.Join('/', segments[..^1]);
        return TryResolveElement(settings, parentPath, out parent, out error);
    }

    private static void SetSingle(XElement settings, string path, string value)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string leaf = segments[^1];
        string parentPath = string.Join('/', segments[..^1]);
        XElement parent = EnsurePath(settings, parentPath);
        XElement? element = parent.Element(leaf);
        if (element is null)
        {
            element = new XElement(leaf);
            parent.Add(element);
        }
        element.Value = value;
        foreach (XElement duplicate in parent.Elements(leaf).Skip(1).ToArray())
            duplicate.Remove();
    }

    private static void SetPathValues(XElement settings, string path, IEnumerable<string> values)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string leaf = segments[^1];
        XElement parent = EnsurePath(settings, string.Join('/', segments[..^1]));
        parent.Elements(leaf).Remove();
        foreach (string value in values.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal))
            parent.Add(new XElement(leaf, value.Trim()));
    }

    private static IReadOnlyList<string> SplitCollection(string value)
        => value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split(['\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out bool parsed) && parsed
            || string.Equals(value?.Trim(), "1", StringComparison.Ordinal)
            || string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value?.Trim(), "x", StringComparison.OrdinalIgnoreCase);

    private static string RedlinerLimb(string control)
        => control switch
        {
            "chkRedlinerLimbsSkull" => "skull",
            "chkRedlinerLimbsTorso" => "torso",
            "chkRedlinerLimbsArms" => "arm",
            "chkRedlinerLimbsLegs" => "leg",
            _ => throw new ArgumentOutOfRangeException(nameof(control), control, "Unknown Redliner control.")
        };

    private static (int Minimum, int Maximum) DecimalPlaces(string format)
    {
        int separator = format.IndexOf('.', StringComparison.Ordinal);
        if (separator < 0)
            return (0, 0);
        string decimalPart = format[(separator + 1)..];
        return (decimalPart.Count(character => character == '0'), decimalPart.Count(character => character is '0' or '#'));
    }

    private static string DecimalFormat(int minimum, int maximum)
        => maximum <= 0 ? "#,0" : $"#,0.{new string('0', minimum)}{new string('#', maximum - minimum)}";

    private static string NormalizeName(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string UniqueProfileName(Chummer5CharacterSettingsCatalog catalog, string requested)
    {
        if (!catalog.Profiles.Any(profile => string.Equals(profile.Name, requested, StringComparison.OrdinalIgnoreCase)))
            return requested;
        for (int index = 2; ; index++)
        {
            string candidate = $"{requested} {index}";
            if (!catalog.Profiles.Any(profile => string.Equals(profile.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
    }

    private static string ReadDefaultValue(Chummer5CharacterSettingsFieldDefinition definition)
    {
        foreach (string path in definition.PersistencePaths)
        {
            if (Chummer5CharacterSettingsRuntimeContractGenerated.BuiltInStandardValues.TryGetValue(path, out IReadOnlyList<string>? values)
                && values.Count > 0)
                return string.Join(", ", values);
        }
        return definition.InputType switch
        {
            "checkbox" => "false",
            "number" => "0",
            _ => string.Empty
        };
    }
}
