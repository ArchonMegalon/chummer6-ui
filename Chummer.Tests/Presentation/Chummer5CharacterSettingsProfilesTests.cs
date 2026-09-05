using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class Chummer5CharacterSettingsProfilesTests
{
    [TestMethod]
    public void Character_settings_phone_sections_reach_every_legacy_value_control()
    {
        IReadOnlyList<Chummer5CharacterSettingsFieldDefinition> definitions =
            Chummer5CharacterSettingsRuntimeContractGenerated.Fields;

        Assert.AreEqual(150, definitions.Count);
        Assert.AreEqual(150, definitions.Select(field => field.LegacyControl).Distinct(StringComparer.Ordinal).Count());
        Assert.IsTrue(Chummer5CharacterSettingsRuntimeContractGenerated.Sections.All(
            section => definitions.Any(field => string.Equals(field.SectionId, section.Id, StringComparison.Ordinal))));

        HashSet<string> reachable = new(StringComparer.Ordinal);
        foreach (Chummer5CharacterSettingsSectionDefinition section in Chummer5CharacterSettingsRuntimeContractGenerated.Sections)
        {
            DesktopDialogState dialog = DesktopDialogFactory.BuildCharacterSettingsDialog(
                DesktopPreferenceState.Default,
                requestedSectionId: section.Id);
            foreach (DesktopDialogField field in dialog.Fields.Where(
                field => field.Id.StartsWith(Chummer5CharacterSettingsProfiles.FieldPrefix, StringComparison.Ordinal)))
            {
                Assert.IsTrue(reachable.Add(field.Id), $"Field {field.Id} was exposed by more than one phone section.");
            }
        }

        CollectionAssert.AreEquivalent(
            definitions.Select(field => Chummer5CharacterSettingsProfiles.FieldId(field.LegacyControl)).ToArray(),
            reachable.ToArray());

        DesktopDialogState initial = DesktopDialogFactory.BuildCharacterSettingsDialog(DesktopPreferenceState.Default);
        Assert.AreEqual("build", DesktopDialogFieldValueParser.GetValue(initial, Chummer5CharacterSettingsProfiles.SectionFieldId));
        CollectionAssert.AreEqual(
            new[] { "save", "save_and_close", "save_as", "rename", "delete", "restore_defaults", "cancel" },
            initial.Actions.Select(action => action.Id).ToArray());
    }

    [TestMethod]
    public void Every_legacy_value_control_round_trips_through_chummer5_settings_xml()
    {
        Chummer5CharacterSettingsProfile standard = Chummer5CharacterSettingsProfiles.ActiveProfile(
            Chummer5CharacterSettingsProfiles.ParseCatalog(null));
        DesktopDialogState dialog = new(
            Chummer5CharacterSettingsProfiles.DialogId,
            "Character Settings",
            null,
            Chummer5CharacterSettingsRuntimeContractGenerated.Fields
                .Select(definition => new DesktopDialogField(
                    Chummer5CharacterSettingsProfiles.FieldId(definition.LegacyControl),
                    definition.Label,
                    TestValue(definition),
                    string.Empty,
                    IsMultiline: definition.IsMultiline,
                    InputType: definition.InputType))
                .Append(new DesktopDialogField(
                    Chummer5CharacterSettingsProfiles.EditedFieldIdsFieldId,
                    "Edited settings fields",
                    string.Join('\n', Chummer5CharacterSettingsRuntimeContractGenerated.Fields.Select(
                        definition => Chummer5CharacterSettingsProfiles.FieldId(definition.LegacyControl))),
                    string.Empty,
                    IsReadOnly: true,
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden))
                .ToArray(),
            []);

        Assert.IsTrue(
            Chummer5CharacterSettingsProfiles.TryApplyVisibleFields(
                dialog,
                standard.Xml,
                out string updatedXml,
                out string? error),
            error);

        foreach (Chummer5CharacterSettingsFieldDefinition definition in Chummer5CharacterSettingsRuntimeContractGenerated.Fields)
        {
            Assert.AreEqual(
                ExpectedReadback(definition),
                Chummer5CharacterSettingsProfiles.ReadFieldValue(updatedXml, definition),
                $"Legacy control {definition.LegacyControl} did not round-trip.");
        }

        XElement settings = XElement.Parse(updatedXml);
        XElement[] customEntries = settings.Element("customdatadirectorynames")!
            .Elements("customdatadirectoryname")
            .ToArray();
        Assert.AreEqual(2, customEntries.Length);
        Assert.AreEqual("alpha", customEntries[0].Element("directoryname")?.Value);
        Assert.AreEqual("1", customEntries[0].Element("order")?.Value);
        Assert.AreEqual("True", customEntries[0].Element("enabled")?.Value);
        Assert.AreEqual("beta", customEntries[1].Element("directoryname")?.Value);
        Assert.AreEqual("2", customEntries[1].Element("order")?.Value);
        Assert.AreEqual("False", customEntries[1].Element("enabled")?.Value);
    }

    [TestMethod]
    public void Profile_actions_preserve_ids_names_xml_and_a_deterministic_fallback()
    {
        Chummer5CharacterSettingsCatalog catalog = Chummer5CharacterSettingsProfiles.ParseCatalog("not json");
        Chummer5CharacterSettingsProfile standard = Chummer5CharacterSettingsProfiles.ActiveProfile(catalog);

        catalog = Chummer5CharacterSettingsProfiles.Save(
            catalog,
            standard.Id,
            "Campaign Standard",
            standard.Xml.Replace("<buildmethod>Priority</buildmethod>", "<buildmethod>Karma</buildmethod>", StringComparison.Ordinal));
        Chummer5CharacterSettingsProfile renamed = Chummer5CharacterSettingsProfiles.ActiveProfile(catalog);
        Assert.AreEqual(standard.Id, renamed.Id);
        Assert.AreEqual("Campaign Standard", renamed.Name);
        Assert.AreEqual("Karma", Chummer5CharacterSettingsProfiles.ReadBuildMethod(renamed.Xml, "Priority"));

        catalog = Chummer5CharacterSettingsProfiles.SaveAs(catalog, "Campaign Standard", renamed.Xml);
        Chummer5CharacterSettingsProfile copy = Chummer5CharacterSettingsProfiles.ActiveProfile(catalog);
        Assert.AreEqual(2, catalog.Profiles.Count);
        Assert.AreNotEqual(renamed.Id, copy.Id);
        Assert.AreEqual("Campaign Standard 2", copy.Name);

        catalog = Chummer5CharacterSettingsProfiles.Delete(catalog, copy.Id);
        Assert.AreEqual(1, catalog.Profiles.Count);
        Assert.AreEqual(renamed.Id, catalog.ActiveProfileId);

        catalog = Chummer5CharacterSettingsProfiles.Delete(catalog, renamed.Id);
        Chummer5CharacterSettingsProfile fallback = Chummer5CharacterSettingsProfiles.ActiveProfile(catalog);
        Assert.AreEqual("Standard", fallback.Name);
        Assert.AreEqual("Priority", Chummer5CharacterSettingsProfiles.ReadBuildMethod(fallback.Xml, "Karma"));

        string restored = Chummer5CharacterSettingsProfiles.RestoreDefaults(renamed.Id, renamed.Name);
        XElement restoredSettings = XElement.Parse(restored);
        Assert.AreEqual(renamed.Id, restoredSettings.Element("id")?.Value);
        Assert.AreEqual(renamed.Name, restoredSettings.Element("name")?.Value);
        Assert.AreEqual("Priority", restoredSettings.Element("buildmethod")?.Value);

        Chummer5CharacterSettingsCatalog serialized = Chummer5CharacterSettingsProfiles.ParseCatalog(
            Chummer5CharacterSettingsProfiles.SerializeCatalog(catalog));
        Assert.AreEqual(catalog.ActiveProfileId, serialized.ActiveProfileId);
        Assert.AreEqual(catalog.Profiles.Count, serialized.Profiles.Count);
    }

    [TestMethod]
    public void Invalid_numeric_values_fail_closed_without_mutating_the_draft()
    {
        Chummer5CharacterSettingsProfile standard = Chummer5CharacterSettingsProfiles.ActiveProfile(
            Chummer5CharacterSettingsProfiles.ParseCatalog(null));
        Chummer5CharacterSettingsFieldDefinition number = Chummer5CharacterSettingsRuntimeContractGenerated.Fields
            .First(field => string.Equals(field.InputType, "number", StringComparison.Ordinal)
                && !field.LegacyControl.StartsWith("nudNuyenDecimals", StringComparison.Ordinal)
                && !string.Equals(field.LegacyControl, "nudEssenceDecimals", StringComparison.Ordinal)
                && !string.Equals(field.LegacyControl, "nudWeightDecimals", StringComparison.Ordinal));
        DesktopDialogState dialog = new(
            Chummer5CharacterSettingsProfiles.DialogId,
            "Character Settings",
            null,
            [new DesktopDialogField(
                Chummer5CharacterSettingsProfiles.FieldId(number.LegacyControl),
                number.Label,
                "not-a-number",
                string.Empty,
                InputType: "number"),
             new DesktopDialogField(
                 Chummer5CharacterSettingsProfiles.EditedFieldIdsFieldId,
                 "Edited settings fields",
                 Chummer5CharacterSettingsProfiles.FieldId(number.LegacyControl),
                 string.Empty,
                 IsReadOnly: true,
                 LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden)],
            []);

        Assert.IsFalse(Chummer5CharacterSettingsProfiles.TryApplyVisibleFields(
            dialog,
            standard.Xml,
            out string updatedXml,
            out string? error));
        Assert.AreEqual(standard.Xml, updatedXml);
        StringAssert.Contains(error ?? string.Empty, "must be a number");
    }

    [TestMethod]
    public void Empty_edited_field_set_is_an_exact_no_op_instead_of_replaying_projected_values()
    {
        string original = "<settings preserve=\"yes\"><buildmethod>Karma</buildmethod><buildpoints>hostile-hidden-value</buildpoints></settings>";
        DesktopDialogState dialog = TrackedDialog(
            Chummer5CharacterSettingsProfiles.FieldId("cboBuildMethod"),
            "Priority",
            editedFieldIds: string.Empty);

        Assert.IsTrue(Chummer5CharacterSettingsProfiles.TryApplyVisibleFields(
            dialog,
            original,
            out string updatedXml,
            out string? error), error);
        Assert.AreEqual(original, updatedXml);
    }

    [TestMethod]
    public void Explicit_partial_update_preserves_hidden_attributes_children_duplicates_namespaces_and_values()
    {
        const string extensionNamespace = "urn:chummer:test-extension";
        string original = $"<?xml version=\"1.0\"?><?keep before?><settings preserve=\"yes\" xmlns:x=\"{extensionNamespace}\">"
            + "<buildmethod marker=\"keep\">Karma</buildmethod>"
            + "<buildpoints marker=\"hidden\"><payload>not-a-number</payload></buildpoints>"
            + "<availability>hostile-a</availability><availability>hostile-b</availability>"
            + "<x:availability x:flag=\"keep\">namespaced-hidden</x:availability>"
            + "<unknown code=\"A\"><child flag=\"keep\">opaque</child></unknown>"
            + "</settings><!--keep-after-->";
        string fieldId = Chummer5CharacterSettingsProfiles.FieldId("cboBuildMethod");

        Assert.IsTrue(Chummer5CharacterSettingsProfiles.TryApplyVisibleFields(
            TrackedDialog(fieldId, "Priority", fieldId),
            original,
            out string updatedXml,
            out string? error), error);

        XDocument document = XDocument.Parse(updatedXml, LoadOptions.PreserveWhitespace);
        XElement settings = document.Root!;
        Assert.IsNotNull(document.Declaration);
        Assert.AreEqual("before", document.Nodes().OfType<XProcessingInstruction>().Single().Data);
        Assert.AreEqual("keep-after", document.Nodes().OfType<XComment>().Single().Value);
        Assert.AreEqual("yes", settings.Attribute("preserve")?.Value);
        Assert.AreEqual("Priority", settings.Element("buildmethod")?.Value);
        Assert.AreEqual("keep", settings.Element("buildmethod")?.Attribute("marker")?.Value);
        Assert.AreEqual("not-a-number", settings.Element("buildpoints")?.Element("payload")?.Value);
        Assert.AreEqual("hidden", settings.Element("buildpoints")?.Attribute("marker")?.Value);
        CollectionAssert.AreEqual(
            new[] { "hostile-a", "hostile-b" },
            settings.Elements("availability").Select(element => element.Value).ToArray());
        Assert.AreEqual(
            "namespaced-hidden",
            settings.Element(XName.Get("availability", extensionNamespace))?.Value);
        Assert.AreEqual("opaque", settings.Element("unknown")?.Element("child")?.Value);
        Assert.AreEqual("keep", settings.Element("unknown")?.Element("child")?.Attribute("flag")?.Value);
    }

    [TestMethod]
    public void Duplicate_explicit_target_fails_closed_with_the_exact_original_draft()
    {
        string original = "<settings><buildmethod>Priority</buildmethod><buildmethod>Karma</buildmethod><hidden keep=\"yes\" /></settings>";
        string fieldId = Chummer5CharacterSettingsProfiles.FieldId("cboBuildMethod");

        Assert.IsFalse(Chummer5CharacterSettingsProfiles.TryApplyVisibleFields(
            TrackedDialog(fieldId, "LifeModule", fieldId),
            original,
            out string updatedXml,
            out string? error));
        Assert.AreEqual(original, updatedXml);
        StringAssert.Contains(error ?? string.Empty, "duplicate targets");
    }

    [TestMethod]
    public void Complex_explicit_target_fails_closed_instead_of_deleting_unknown_children()
    {
        string original = "<settings><buildmethod marker=\"keep\"><unknown>Priority</unknown></buildmethod></settings>";
        string fieldId = Chummer5CharacterSettingsProfiles.FieldId("cboBuildMethod");

        Assert.IsFalse(Chummer5CharacterSettingsProfiles.TryApplyVisibleFields(
            TrackedDialog(fieldId, "Karma", fieldId),
            original,
            out string updatedXml,
            out string? error));
        Assert.AreEqual(original, updatedXml);
        StringAssert.Contains(error ?? string.Empty, "complex content");
    }

    [DataTestMethod]
    [DataRow("<setting><buildmethod>Priority</buildmethod></setting>")]
    [DataRow("<settings xmlns=\"urn:chummer:test\"><buildmethod>Priority</buildmethod></settings>")]
    [DataRow("<settings><x:buildmethod xmlns:x=\"urn:chummer:test\">Priority</x:buildmethod></settings>")]
    [DataRow("<settings><buildmethod>Priority</settings>")]
    public void Malformed_root_or_namespace_mismatch_fails_closed_with_the_exact_original_draft(string original)
    {
        string fieldId = Chummer5CharacterSettingsProfiles.FieldId("cboBuildMethod");

        Assert.IsFalse(Chummer5CharacterSettingsProfiles.TryApplyVisibleFields(
            TrackedDialog(fieldId, "Karma", fieldId),
            original,
            out string updatedXml,
            out _));
        Assert.AreEqual(original, updatedXml);
    }

    [TestMethod]
    public void Catalog_restart_preserves_malformed_xml_for_fail_closed_recovery_instead_of_inventing_defaults()
    {
        const string malformed = "<settings><buildmethod>Priority</settings>";
        var catalog = new Chummer5CharacterSettingsCatalog(
            "profile-1",
            [new Chummer5CharacterSettingsProfile("profile-1", "Broken profile", malformed)]);

        Chummer5CharacterSettingsCatalog restarted = Chummer5CharacterSettingsProfiles.ParseCatalog(
            Chummer5CharacterSettingsProfiles.SerializeCatalog(catalog));

        Assert.AreEqual(1, restarted.Profiles.Count);
        Assert.AreEqual(malformed, restarted.Profiles.Single().Xml);
        Assert.IsFalse(Chummer5CharacterSettingsProfiles.TryApplyVisibleFields(
            TrackedDialog(
                Chummer5CharacterSettingsProfiles.FieldId("cboBuildMethod"),
                "Karma",
                Chummer5CharacterSettingsProfiles.FieldId("cboBuildMethod")),
            restarted.Profiles.Single().Xml,
            out string updatedXml,
            out _));
        Assert.AreEqual(malformed, updatedXml);
    }

    [TestMethod]
    public void Partial_save_and_restart_preserve_hostile_xml_without_duplicating_the_profile()
    {
        string original = "<settings preserve=\"yes\"><id>profile-1</id><name>Inner name</name>"
            + "<buildmethod marker=\"keep\">Karma</buildmethod>"
            + "<buildpoints><unknown>noncanonical-hidden</unknown></buildpoints>"
            + "<availability>first</availability><availability>second</availability>"
            + "</settings>";
        var catalog = new Chummer5CharacterSettingsCatalog(
            "profile-1",
            [new Chummer5CharacterSettingsProfile("profile-1", "Outer name", original)]);
        string fieldId = Chummer5CharacterSettingsProfiles.FieldId("cboBuildMethod");

        Assert.IsTrue(Chummer5CharacterSettingsProfiles.TryApplyVisibleFields(
            TrackedDialog(fieldId, "Priority", fieldId),
            original,
            out string updatedXml,
            out string? error), error);
        catalog = Chummer5CharacterSettingsProfiles.Save(
            catalog,
            "profile-1",
            "Renamed outer profile",
            updatedXml);
        Chummer5CharacterSettingsCatalog restarted = Chummer5CharacterSettingsProfiles.ParseCatalog(
            Chummer5CharacterSettingsProfiles.SerializeCatalog(catalog));

        Assert.AreEqual(1, restarted.Profiles.Count);
        Chummer5CharacterSettingsProfile profile = restarted.Profiles.Single();
        Assert.AreEqual("profile-1", profile.Id);
        Assert.AreEqual("Renamed outer profile", profile.Name);
        XElement settings = XElement.Parse(profile.Xml, LoadOptions.PreserveWhitespace);
        Assert.AreEqual("yes", settings.Attribute("preserve")?.Value);
        Assert.AreEqual("Priority", settings.Element("buildmethod")?.Value);
        Assert.AreEqual("keep", settings.Element("buildmethod")?.Attribute("marker")?.Value);
        Assert.AreEqual("noncanonical-hidden", settings.Element("buildpoints")?.Element("unknown")?.Value);
        CollectionAssert.AreEqual(
            new[] { "first", "second" },
            settings.Elements("availability").Select(element => element.Value).ToArray());

        Chummer5CharacterSettingsCatalog savedAgain = Chummer5CharacterSettingsProfiles.Save(
            restarted,
            profile.Id,
            profile.Name,
            profile.Xml);
        Assert.AreEqual(1, Chummer5CharacterSettingsProfiles.ParseCatalog(
            Chummer5CharacterSettingsProfiles.SerializeCatalog(savedAgain)).Profiles.Count);
    }

    private static DesktopDialogState TrackedDialog(
        string fieldId,
        string value,
        string? editedFieldIds)
        => new(
            Chummer5CharacterSettingsProfiles.DialogId,
            "Character Settings",
            null,
            [new DesktopDialogField(fieldId, fieldId, value, string.Empty),
             new DesktopDialogField(
                 Chummer5CharacterSettingsProfiles.EditedFieldIdsFieldId,
                 "Edited settings fields",
                 editedFieldIds ?? string.Empty,
                 string.Empty,
                 IsReadOnly: true,
                 LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden)],
            []);

    private static string TestValue(Chummer5CharacterSettingsFieldDefinition definition)
        => definition.LegacyControl switch
        {
            "treSourcebook" => "SR5\nRun Faster",
            "chkGrade" => "Standard\nAlpha",
            "treCustomDataDirectories" => "[x] alpha\n[ ] beta",
            "cboLimbCount" => "5<torso",
            "nudNuyenDecimalsMinimum" => "2",
            "nudNuyenDecimalsMaximum" => "4",
            "nudEssenceDecimals" => "3",
            "nudWeightDecimals" => "2",
            _ when string.Equals(definition.InputType, "checkbox", StringComparison.Ordinal) => "true",
            _ when string.Equals(definition.InputType, "number", StringComparison.Ordinal) => "3",
            _ when definition.Options.Count > 0 => definition.Options[0],
            _ => "sample"
        };

    private static string ExpectedReadback(Chummer5CharacterSettingsFieldDefinition definition)
        => definition.LegacyControl switch
        {
            "treSourcebook" => string.Join(Environment.NewLine, "SR5", "Run Faster"),
            "chkGrade" => string.Join(Environment.NewLine, "Standard", "Alpha"),
            "treCustomDataDirectories" => string.Join(Environment.NewLine, "[x] alpha", "[ ] beta"),
            _ => TestValue(definition)
        };
}
