#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class WorkspaceXmlMutationCatalogTests
{
    private const string ResolverVehicleModId = "f89a112e-600a-4278-8731-9b14cf3737c9";

    [TestMethod]
    public void ApplyConditionMonitorEdit_updates_only_the_selected_career_track()
    {
        const string xml = """
            <character>
              <created>True</created>
              <alias>Preserve me</alias>
              <physicalcm>11</physicalcm>
              <physicalcmoverflow>3</physicalcmoverflow>
              <physicalcmfilled>2</physicalcmfilled>
              <stuncm>10</stuncm>
              <stuncmfilled>4</stuncmfilled>
            </character>
            """;

        string mutated = WorkspaceXmlMutationCatalog.ApplyConditionMonitorEdit(
            xml,
            new ConditionMonitorEditRequest(WorkspaceConditionMonitorTrack.Physical, 12));
        XElement root = XDocument.Parse(mutated).Root!;

        Assert.AreEqual("12", root.Element("physicalcmfilled")!.Value);
        Assert.AreEqual("4", root.Element("stuncmfilled")!.Value);
        Assert.AreEqual("Preserve me", root.Element("alias")!.Value);
    }

    [TestMethod]
    public void ApplyConditionMonitorEdit_rejects_creation_and_out_of_range_damage()
    {
        const string creation = """
            <character>
              <created>False</created>
              <physicalcm>10</physicalcm>
              <physicalcmfilled>0</physicalcmfilled>
            </character>
            """;
        const string career = """
            <character>
              <created>True</created>
              <stuncm>10</stuncm>
              <stuncmfilled>0</stuncmfilled>
            </character>
            """;

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyConditionMonitorEdit(
            creation,
            new ConditionMonitorEditRequest(WorkspaceConditionMonitorTrack.Physical, 1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyConditionMonitorEdit(
            career,
            new ConditionMonitorEditRequest(WorkspaceConditionMonitorTrack.Stun, 11)));
    }

    [TestMethod]
    public void CareerReputationEditor_projects_exact_values_and_sourcebook_visibility()
    {
        const string xml = "<character><created>True</created><streetcred>11</streetcred><notoriety>12</notoriety><publicawareness>13</publicawareness><baseastralreputation>14</baseastralreputation><basewildreputation>15</basewildreputation></character>";
        CharacterWorkspaceId workspaceId = new("career-reputation");

        CareerReputationEditorState coreOnly = CareerReputationEditorProjector.Project(
            xml,
            workspaceId,
            7,
            new BookSourceDataResolver("SR5"));
        CareerReputationEditorState streetGrimoire = CareerReputationEditorProjector.Project(
            xml,
            workspaceId,
            7,
            new BookSourceDataResolver("SG"));
        CareerReputationEditorState forbiddenArcana = CareerReputationEditorProjector.Project(
            xml,
            workspaceId,
            7,
            new BookSourceDataResolver("FA"));

        Assert.AreEqual(11, coreOnly.StreetCred);
        Assert.AreEqual(12, coreOnly.Notoriety);
        Assert.AreEqual(13, coreOnly.PublicAwareness);
        Assert.AreEqual(14, coreOnly.AstralReputation);
        Assert.AreEqual(15, coreOnly.WildReputation);
        Assert.IsFalse(coreOnly.AstralReputationVisible);
        Assert.IsFalse(coreOnly.WildReputationVisible);
        Assert.IsTrue(streetGrimoire.AstralReputationVisible);
        Assert.IsFalse(streetGrimoire.WildReputationVisible);
        Assert.IsTrue(forbiddenArcana.AstralReputationVisible);
        Assert.IsTrue(forbiddenArcana.WildReputationVisible);
    }

    [TestMethod]
    public void ApplyCareerReputationEdit_updates_five_exact_career_fields()
    {
        const string xml = "<character><created>True</created><alias>Preserve me</alias><streetcred>1</streetcred><notoriety>2</notoriety><publicawareness>3</publicawareness><baseastralreputation>4</baseastralreputation><basewildreputation>5</basewildreputation></character>";
        CareerReputationEditRequest request = new(
            new CharacterWorkspaceId("career-reputation"),
            ExpectedContentRevision: 7,
            StreetCred: 21,
            Notoriety: 22,
            PublicAwareness: 23,
            AstralReputation: 24,
            WildReputation: 25);

        string mutated = WorkspaceXmlMutationCatalog.ApplyCareerReputationEdit(
            xml,
            request,
            new BookSourceDataResolver("FA"));
        XElement root = XDocument.Parse(mutated).Root!;

        Assert.AreEqual("21", root.Element("streetcred")!.Value);
        Assert.AreEqual("22", root.Element("notoriety")!.Value);
        Assert.AreEqual("23", root.Element("publicawareness")!.Value);
        Assert.AreEqual("24", root.Element("baseastralreputation")!.Value);
        Assert.AreEqual("25", root.Element("basewildreputation")!.Value);
        Assert.AreEqual("Preserve me", root.Element("alias")!.Value);
    }

    [TestMethod]
    public void ApplyCareerReputationEdit_rejects_creation_bounds_and_unavailable_sources()
    {
        CharacterWorkspaceId workspaceId = new("career-reputation");
        CareerReputationEditRequest baseRequest = new(workspaceId, 7, 1, 2, 3, null, null);
        const string creation = "<character><created>False</created></character>";
        const string career = "<character><created>True</created></character>";

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCareerReputationEdit(
            creation,
            baseRequest,
            new BookSourceDataResolver("FA")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCareerReputationEdit(
            career,
            baseRequest with { StreetCred = 101 },
            new BookSourceDataResolver("FA")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCareerReputationEdit(
            career,
            baseRequest with { AstralReputation = 4 },
            new BookSourceDataResolver("SR5")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCareerReputationEdit(
            career,
            baseRequest with { WildReputation = 5 },
            new BookSourceDataResolver("SG")));

        string baseOnly = WorkspaceXmlMutationCatalog.ApplyCareerReputationEdit(career, baseRequest, null);
        StringAssert.Contains(baseOnly, "<streetcred>1</streetcred>");
        Assert.IsNull(XDocument.Parse(baseOnly).Root!.Element("baseastralreputation"));
    }

    [TestMethod]
    public void CareerReputationEditor_projects_exact_burn_eligibility_from_career_karma_and_improvements()
    {
        const string xml = """
            <character>
              <created>True</created>
              <streetcred>1</streetcred>
              <burntstreetcred>2</burntstreetcred>
              <expenses>
                <expense><amount>19</amount><type>Karma</type><refund>False</refund></expense>
                <expense><amount>-3</amount><type>Karma</type><refund>False</refund><forcecareervisible>True</forcecareervisible></expense>
                <expense><amount>99</amount><type>Karma</type><refund>True</refund></expense>
                <expense><amount>99</amount><type>Nuyen</type><refund>False</refund></expense>
              </expenses>
              <improvements>
                <improvement><improvementttype>StreetCredMultiplier</improvementttype><val>1.2</val><enabled>1</enabled></improvement>
                <improvement><improvementttype>StreetCred</improvementttype><val>1.1</val><enabled>1</enabled></improvement>
                <improvement><improvementttype>StreetCred</improvementttype><unique>same-source</unique><val>5</val><enabled>1</enabled></improvement>
                <improvement><improvementttype>StreetCred</improvementttype><unique>same-source</unique><val>3</val><enabled>1</enabled></improvement>
                <improvement><improvementttype>StreetCred</improvementttype><val>100</val><enabled>0</enabled></improvement>
                <improvement><improvementttype>StreetCred</improvementttype><val>100</val><enabled>1</enabled><condition>create</condition></improvement>
              </improvements>
            </character>
            """;

        CareerReputationEditorState editor = CareerReputationEditorProjector.Project(
            xml,
            new CharacterWorkspaceId("burn-street-cred"),
            9,
            sourceDataResolver: null);

        Assert.AreEqual(2, editor.BurntStreetCred);
        Assert.AreEqual(7, editor.TotalStreetCred);
        Assert.IsTrue(editor.CanBurnStreetCred);
        Assert.IsNull(editor.BurnStreetCredUnavailableReason);
    }

    [TestMethod]
    public void ApplyBurnStreetCred_increments_only_burnt_value_and_revalidates_current_xml()
    {
        CharacterWorkspaceId workspaceId = new("burn-street-cred");
        BurnStreetCredRequest request = new(workspaceId, ExpectedContentRevision: 9);
        const string eligible = "<character><created>True</created><alias>Preserve me</alias><streetcred>4</streetcred><burntstreetcred>1</burntstreetcred></character>";

        string mutated = WorkspaceXmlMutationCatalog.ApplyBurnStreetCred(eligible, request);
        XElement root = XDocument.Parse(mutated).Root!;

        Assert.AreEqual("3", root.Element("burntstreetcred")!.Value);
        Assert.AreEqual("4", root.Element("streetcred")!.Value);
        Assert.AreEqual("Preserve me", root.Element("alias")!.Value);

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyBurnStreetCred(
            "<character><created>False</created><streetcred>10</streetcred></character>",
            request));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyBurnStreetCred(
            "<character><created>True</created><streetcred>1</streetcred></character>",
            request));
    }

    [TestMethod]
    public void SituationalModifiersEditor_projects_exact_creation_and_career_values()
    {
        CharacterWorkspaceId workspaceId = new("situational-modifiers");
        foreach (bool created in new[] { false, true })
        {
            string xml = $"<character><created>{created}</created><currentcounterspellingdice>17</currentcounterspellingdice><currentliftcarryhits>18</currentliftcarryhits></character>";

            SituationalModifiersEditorState editor = SituationalModifiersEditorProjector.Project(
                xml,
                workspaceId,
                9);

            Assert.AreEqual(workspaceId, editor.WorkspaceId);
            Assert.AreEqual(9, editor.ContentRevision);
            Assert.AreEqual(17, editor.CounterspellingDice);
            Assert.AreEqual(18, editor.LiftCarryHits);
        }
    }

    [TestMethod]
    public void ApplySituationalModifiersEdit_updates_exact_fields_for_creation_and_career()
    {
        CharacterWorkspaceId workspaceId = new("situational-modifiers");
        SituationalModifiersEditRequest request = new(workspaceId, 9, 31, 32);
        foreach (bool created in new[] { false, true })
        {
            string xml = $"<character><created>{created}</created><alias>Preserve me</alias><currentcounterspellingdice>1</currentcounterspellingdice><currentliftcarryhits>2</currentliftcarryhits></character>";

            XElement root = XDocument.Parse(
                WorkspaceXmlMutationCatalog.ApplySituationalModifiersEdit(xml, request)).Root!;

            Assert.AreEqual("31", root.Element("currentcounterspellingdice")!.Value);
            Assert.AreEqual("32", root.Element("currentliftcarryhits")!.Value);
            Assert.AreEqual("Preserve me", root.Element("alias")!.Value);
            Assert.AreEqual(created.ToString(), root.Element("created")!.Value);
        }
    }

    [TestMethod]
    public void SituationalModifiers_reject_invalid_revision_values_and_bounds()
    {
        CharacterWorkspaceId workspaceId = new("situational-modifiers");
        const string xml = "<character><currentcounterspellingdice>1</currentcounterspellingdice><currentliftcarryhits>2</currentliftcarryhits></character>";

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SituationalModifiersEditorProjector.Project(xml, workspaceId, 0));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SituationalModifiersEditorProjector.Project(
                "<character><currentcounterspellingdice>101</currentcounterspellingdice></character>",
                workspaceId,
                9));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplySituationalModifiersEdit(
                xml,
                new SituationalModifiersEditRequest(workspaceId, 9, -1, 2)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplySituationalModifiersEdit(
                xml,
                new SituationalModifiersEditRequest(workspaceId, 9, 1, 101)));
    }

    [TestMethod]
    public void PrimaryArmEditor_projects_legacy_default_and_ambidextrous_gate()
    {
        CharacterWorkspaceId workspaceId = new("primary-arm");
        const string ambidextrous = "<character><primaryarm>Left</primaryarm><improvements><improvement><improvementttype>Ambidextrous</improvementttype><enabled>True</enabled></improvement></improvements></character>";

        PrimaryArmEditorState editable = PrimaryArmEditorProjector.Project(
            "<character />",
            workspaceId,
            11);
        PrimaryArmEditorState readOnly = PrimaryArmEditorProjector.Project(
            ambidextrous,
            workspaceId,
            11);

        Assert.AreEqual("Right", editable.Value);
        Assert.IsFalse(editable.Ambidextrous);
        Assert.AreEqual("Left", readOnly.Value);
        Assert.IsTrue(readOnly.Ambidextrous);
    }

    [TestMethod]
    public void ApplyPrimaryArmEdit_updates_creation_and_career_without_touching_unrelated_xml()
    {
        PrimaryArmEditRequest request = new(new CharacterWorkspaceId("primary-arm"), 11, "Left");
        foreach (bool created in new[] { false, true })
        {
            string xml = $"<character><created>{created}</created><primaryarm>Right</primaryarm><custom><keep>verbatim</keep></custom></character>";

            XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyPrimaryArmEdit(xml, request)).Root!;

            Assert.AreEqual("Left", root.Element("primaryarm")!.Value);
            Assert.AreEqual("verbatim", root.Element("custom")!.Element("keep")!.Value);
            Assert.AreEqual(created.ToString(), root.Element("created")!.Value);
        }
    }

    [TestMethod]
    public void ApplyPrimaryArmEdit_rejects_nonlegacy_values_and_ambidextrous_runners()
    {
        CharacterWorkspaceId workspaceId = new("primary-arm");
        const string ambidextrous = "<character><primaryarm>Right</primaryarm><improvements><improvement><improvementttype>Ambidextrous</improvementttype><enabled>1</enabled></improvement></improvements></character>";
        const string disabled = "<character><primaryarm>Right</primaryarm><improvements><improvement><improvementttype>Ambidextrous</improvementttype><enabled>False</enabled></improvement></improvements></character>";

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyPrimaryArmEdit(
            "<character><primaryarm>Right</primaryarm></character>",
            new PrimaryArmEditRequest(workspaceId, 11, "Center")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyPrimaryArmEdit(
            ambidextrous,
            new PrimaryArmEditRequest(workspaceId, 11, "Left")));
        StringAssert.Contains(
            WorkspaceXmlMutationCatalog.ApplyPrimaryArmEdit(
                disabled,
                new PrimaryArmEditRequest(workspaceId, 11, "Left")),
            "<primaryarm>Left</primaryarm>");
    }

    [TestMethod]
    public void ApplyGearLocationAdd_appends_exact_legacy_location_for_creation_and_career()
    {
        CharacterWorkspaceId workspaceId = new("gear-location");
        GearLocationAddRequest request = new(workspaceId, 17, "  Field Kit  ");
        foreach (bool created in new[] { false, true })
        {
            string xml = $"<character><created>{created}</created><alias>Preserve me</alias><gearlocations><location><guid>11111111-1111-1111-1111-111111111111</guid><name>Existing</name><notes /></location></gearlocations></character>";

            XElement root = XDocument.Parse(
                WorkspaceXmlMutationCatalog.ApplyGearLocationAdd(xml, request)).Root!;
            XElement[] locations = root.Element("gearlocations")!.Elements("location").ToArray();

            Assert.HasCount(2, locations);
            Assert.AreEqual("Existing", locations[0].Element("name")!.Value);
            Assert.IsTrue(Guid.TryParseExact(locations[1].Element("guid")!.Value, "D", out _));
            Assert.AreEqual("  Field Kit  ", locations[1].Element("name")!.Value);
            Assert.AreEqual(string.Empty, locations[1].Element("notes")!.Value);
            CollectionAssert.AreEqual(
                new[] { "guid", "name", "notes" },
                locations[1].Elements().Select(element => element.Name.LocalName).ToArray());
            Assert.AreEqual("Preserve me", root.Element("alias")!.Value);
            Assert.AreEqual(created.ToString(), root.Element("created")!.Value);
        }
    }

    [TestMethod]
    public void ApplyGearLocationAdd_creates_missing_container_and_rejects_invalid_names()
    {
        CharacterWorkspaceId workspaceId = new("gear-location");
        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyGearLocationAdd(
            "<character><alias>Preserve me</alias></character>",
            new GearLocationAddRequest(workspaceId, 17, "Satchel"))).Root!;

        Assert.AreEqual("Satchel", root.Element("gearlocations")!.Element("location")!.Element("name")!.Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyGearLocationAdd(
            "<character />",
            new GearLocationAddRequest(workspaceId, 17, string.Empty)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyGearLocationAdd(
            "<character />",
            new GearLocationAddRequest(
                workspaceId,
                17,
                new string('x', GearLocationAddRequest.MaximumNameLength + 1))));
    }

    [TestMethod]
    public void ApplyWeaponLocationAdd_appends_exact_legacy_location_for_creation_and_career()
    {
        CharacterWorkspaceId workspaceId = new("weapon-location");
        WeaponLocationAddRequest request = new(workspaceId, 19, "  Armory Rack  ");
        foreach (bool created in new[] { false, true })
        {
            string xml = $"<character><created>{created}</created><alias>Preserve me</alias><weaponlocations><location><guid>22222222-2222-2222-2222-222222222222</guid><name>Existing</name><notes>Existing notes</notes></location></weaponlocations></character>";

            XElement root = XDocument.Parse(
                WorkspaceXmlMutationCatalog.ApplyWeaponLocationAdd(xml, request)).Root!;
            XElement[] locations = root.Element("weaponlocations")!.Elements("location").ToArray();

            Assert.HasCount(2, locations);
            Assert.AreEqual("Existing", locations[0].Element("name")!.Value);
            Assert.AreEqual("Existing notes", locations[0].Element("notes")!.Value);
            Assert.IsTrue(Guid.TryParseExact(locations[1].Element("guid")!.Value, "D", out _));
            Assert.AreEqual("  Armory Rack  ", locations[1].Element("name")!.Value);
            Assert.AreEqual(string.Empty, locations[1].Element("notes")!.Value);
            CollectionAssert.AreEqual(
                new[] { "guid", "name", "notes" },
                locations[1].Elements().Select(element => element.Name.LocalName).ToArray());
            Assert.AreEqual("Preserve me", root.Element("alias")!.Value);
            Assert.AreEqual(created.ToString(), root.Element("created")!.Value);
        }
    }

    [TestMethod]
    public void ApplyWeaponLocationAdd_creates_missing_container_and_rejects_invalid_names()
    {
        CharacterWorkspaceId workspaceId = new("weapon-location");
        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyWeaponLocationAdd(
            "<character><alias>Preserve me</alias></character>",
            new WeaponLocationAddRequest(workspaceId, 19, "Safehouse"))).Root!;

        Assert.AreEqual("Safehouse", root.Element("weaponlocations")!.Element("location")!.Element("name")!.Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyWeaponLocationAdd(
            "<character />",
            new WeaponLocationAddRequest(workspaceId, 19, string.Empty)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyWeaponLocationAdd(
            "<character />",
            new WeaponLocationAddRequest(
                workspaceId,
                19,
                new string('x', WeaponLocationAddRequest.MaximumNameLength + 1))));
    }

    [TestMethod]
    public void ApplyVehicleLocationAdd_matches_global_and_selected_vehicle_legacy_branches_in_both_modes()
    {
        CharacterWorkspaceId workspaceId = new("vehicle-location");
        Guid targetId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        foreach (bool created in new[] { false, true })
        {
            string xml = $"""
                <character>
                  <created>{created}</created>
                  <alias>Preserve me</alias>
                  <vehiclelocations>
                    <location><guid>11111111-1111-1111-1111-111111111111</guid><name>Global existing</name><notes>Global notes</notes></location>
                  </vehiclelocations>
                  <vehicles>
                    <vehicle>
                      <guid>{targetId:D}</guid><name>Roadmaster</name><custom>target preserved</custom>
                      <locations><location><guid>22222222-2222-2222-2222-222222222222</guid><name>Nested existing</name><notes>Nested notes</notes></location></locations>
                    </vehicle>
                    <vehicle><guid>44444444-4444-4444-4444-444444444444</guid><name>Other</name><locations /></vehicle>
                  </vehicles>
                </character>
                """;

            XElement globalRoot = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyVehicleLocationAdd(
                xml,
                new VehicleLocationAddRequest(workspaceId, 23, null, "  Team garage  "))).Root!;
            XElement[] global = globalRoot.Element("vehiclelocations")!.Elements("location").ToArray();
            Assert.HasCount(2, global);
            Assert.AreEqual("Global notes", global[0].Element("notes")!.Value);
            Assert.AreEqual("  Team garage  ", global[1].Element("name")!.Value);
            Assert.IsTrue(Guid.TryParseExact(global[1].Element("guid")!.Value, "D", out _));
            Assert.AreEqual(string.Empty, global[1].Element("notes")!.Value);
            Assert.HasCount(1, globalRoot.Element("vehicles")!.Elements("vehicle").First().Element("locations")!.Elements("location").ToArray());

            XElement selectedRoot = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyVehicleLocationAdd(
                xml,
                new VehicleLocationAddRequest(workspaceId, 23, targetId, "  Smuggling bay  "))).Root!;
            XElement target = selectedRoot.Element("vehicles")!.Elements("vehicle").First();
            XElement[] nested = target.Element("locations")!.Elements("location").ToArray();
            Assert.HasCount(2, nested);
            Assert.AreEqual("Nested notes", nested[0].Element("notes")!.Value);
            Assert.AreEqual("  Smuggling bay  ", nested[1].Element("name")!.Value);
            Assert.IsTrue(Guid.TryParseExact(nested[1].Element("guid")!.Value, "D", out _));
            Assert.AreEqual(string.Empty, nested[1].Element("notes")!.Value);
            Assert.AreEqual("target preserved", target.Element("custom")!.Value);
            Assert.HasCount(1, selectedRoot.Element("vehiclelocations")!.Elements("location").ToArray());
            Assert.HasCount(0, selectedRoot.Element("vehicles")!.Elements("vehicle").Last().Element("locations")!.Elements("location").ToArray());
            Assert.AreEqual("Preserve me", selectedRoot.Element("alias")!.Value);
            Assert.AreEqual(created.ToString(), selectedRoot.Element("created")!.Value);
        }
    }

    [TestMethod]
    public void ApplyVehicleHomeNodeEdit_enforces_single_home_node_and_preserves_unrelated_xml_in_both_modes()
    {
        CharacterWorkspaceId workspaceId = new("vehicle-home-node");
        Guid targetId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        foreach (bool created in new[] { false, true })
        {
            string xml = $"""
                <character>
                  <created>{created}</created><alias>Preserve me</alias>
                  <gears><gear><guid>11111111-1111-1111-1111-111111111111</guid><homenode>True</homenode><custom>gear preserved</custom></gear></gears>
                  <armors><armor><guid>22222222-2222-2222-2222-222222222222</guid><homenode>False</homenode></armor></armors>
                  <vehicles>
                    <vehicle><guid>{targetId:D}</guid><name>Roadmaster</name><homenode>False</homenode><custom>target preserved</custom></vehicle>
                    <vehicle><guid>44444444-4444-4444-4444-444444444444</guid><name>Other</name><homenode>False</homenode><custom>other preserved</custom></vehicle>
                  </vehicles>
                </character>
                """;

            XElement enabled = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyVehicleHomeNodeEdit(
                xml,
                new VehicleHomeNodeEditRequest(workspaceId, 23, targetId, true))).Root!;
            XElement target = enabled.Element("vehicles")!.Elements("vehicle").First();
            Assert.AreEqual("True", target.Element("homenode")!.Value);
            Assert.AreEqual("False", enabled.Element("gears")!.Element("gear")!.Element("homenode")!.Value);
            Assert.AreEqual("False", enabled.Element("vehicles")!.Elements("vehicle").Last().Element("homenode")!.Value);
            Assert.AreEqual("target preserved", target.Element("custom")!.Value);
            Assert.AreEqual("Preserve me", enabled.Element("alias")!.Value);

            XElement disabled = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyVehicleHomeNodeEdit(
                enabled.ToString(SaveOptions.DisableFormatting),
                new VehicleHomeNodeEditRequest(workspaceId, 24, targetId, false))).Root!;
            Assert.AreEqual(
                "False",
                disabled.Element("vehicles")!.Elements("vehicle").First().Element("homenode")!.Value);
            Assert.IsFalse(disabled.Descendants("homenode").Any(node => bool.Parse(node.Value)));
        }
    }

    [TestMethod]
    public void ApplyVehicleHomeNodeEdit_creates_target_flag_and_rejects_ambiguous_or_invalid_identity()
    {
        CharacterWorkspaceId workspaceId = new("vehicle-home-node");
        Guid targetId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        string missingFlag = $"""
            <character><vehicles><vehicle><guid>{targetId:D}</guid><name>Roadmaster</name></vehicle></vehicles></character>
            """;

        XElement created = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyVehicleHomeNodeEdit(
            missingFlag,
            new VehicleHomeNodeEditRequest(workspaceId, 7, targetId, true))).Root!;
        Assert.AreEqual("True", created.Descendants("vehicle").Single().Element("homenode")!.Value);

        string duplicateVehicle = $"""
            <character><vehicles>
              <vehicle><guid>{targetId:D}</guid><homenode>False</homenode></vehicle>
              <vehicle><guid>{targetId:D}</guid><homenode>False</homenode></vehicle>
            </vehicles></character>
            """;
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyVehicleHomeNodeEdit(
            duplicateVehicle,
            new VehicleHomeNodeEditRequest(workspaceId, 7, targetId, true)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyVehicleHomeNodeEdit(
            missingFlag,
            new VehicleHomeNodeEditRequest(workspaceId, 7, Guid.Empty, true)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyVehicleHomeNodeEdit(
            $"<character><vehicles><vehicle><guid>{targetId:D}</guid><homenode>not-bool</homenode></vehicle></vehicles></character>",
            new VehicleHomeNodeEditRequest(workspaceId, 7, targetId, true)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyVehicleHomeNodeEdit(
            $"<character><vehicles><vehicle><guid>{targetId:D}</guid><homenode>True</homenode><homenode>False</homenode></vehicle></vehicles></character>",
            new VehicleHomeNodeEditRequest(workspaceId, 7, targetId, true)));
    }

    [TestMethod]
    public void ApplyVehicleLocationAdd_creates_either_container_and_rejects_ambiguous_or_invalid_targets()
    {
        CharacterWorkspaceId workspaceId = new("vehicle-location");
        Guid targetId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        XElement global = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyVehicleLocationAdd(
            "<character><alias>Preserve me</alias></character>",
            new VehicleLocationAddRequest(workspaceId, 23, null, "Garage"))).Root!;
        Assert.AreEqual("Garage", global.Element("vehiclelocations")!.Element("location")!.Element("name")!.Value);

        XElement nested = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyVehicleLocationAdd(
            $"<character><vehicles><vehicle><guid>{targetId:D}</guid><name>Roadmaster</name></vehicle></vehicles></character>",
            new VehicleLocationAddRequest(workspaceId, 23, targetId, "Bay"))).Root!;
        Assert.AreEqual("Bay", nested.Element("vehicles")!.Element("vehicle")!.Element("locations")!.Element("location")!.Element("name")!.Value);

        string duplicate = $"<character><vehicles><vehicle><guid>{targetId:D}</guid></vehicle><vehicle><guid>{targetId:D}</guid></vehicle></vehicles></character>";
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyVehicleLocationAdd(
            duplicate,
            new VehicleLocationAddRequest(workspaceId, 23, targetId, "Bay")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyVehicleLocationAdd(
            "<character><vehiclelocations /><vehiclelocations /></character>",
            new VehicleLocationAddRequest(workspaceId, 23, null, "Garage")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyVehicleLocationAdd(
            $"<character><vehicles><vehicle><guid>{targetId:D}</guid><locations /><locations /></vehicle></vehicles></character>",
            new VehicleLocationAddRequest(workspaceId, 23, targetId, "Bay")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyVehicleLocationAdd(
            "<character><vehicles /></character>",
            new VehicleLocationAddRequest(workspaceId, 23, targetId, "Bay")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyVehicleLocationAdd(
            "<character />",
            new VehicleLocationAddRequest(workspaceId, 23, null, string.Empty)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyVehicleLocationAdd(
            "<character />",
            new VehicleLocationAddRequest(
                workspaceId,
                23,
                null,
                new string('x', VehicleLocationAddRequest.MaximumNameLength + 1))));
    }

    [TestMethod]
    public void ApplyLocationRename_updates_only_the_stable_target_for_all_kinds_and_modes()
    {
        CharacterWorkspaceId workspaceId = new("location-rename");
        (WorkspaceLocationKind Kind, string Container, Guid Target)[] kinds =
        [
            (WorkspaceLocationKind.Gear, "gearlocations", Guid.Parse("11111111-1111-1111-1111-111111111111")),
            (WorkspaceLocationKind.Weapon, "weaponlocations", Guid.Parse("22222222-2222-2222-2222-222222222222")),
            (WorkspaceLocationKind.Armor, "armorlocations", Guid.Parse("33333333-3333-3333-3333-333333333333")),
            (WorkspaceLocationKind.Vehicle, "vehiclelocations", Guid.Parse("44444444-4444-4444-4444-444444444444"))
        ];
        const string containers = """
<gearlocations><location><guid>11111111-1111-1111-1111-111111111111</guid><name>Gear old</name><notes>Gear notes</notes></location></gearlocations>
<weaponlocations><location><guid>22222222-2222-2222-2222-222222222222</guid><name>Weapon old</name><notes>Weapon notes</notes></location></weaponlocations>
<armorlocations><location><guid>33333333-3333-3333-3333-333333333333</guid><name>Armor old</name><notes>Armor notes</notes></location></armorlocations>
<vehiclelocations><location><guid>44444444-4444-4444-4444-444444444444</guid><name>Vehicle old</name><notes>Vehicle notes</notes></location></vehiclelocations>
""";

        foreach (bool created in new[] { false, true })
        {
            foreach ((WorkspaceLocationKind kind, string container, Guid target) in kinds)
            {
                string xml = $"<character><created>{created}</created><alias>Preserve me</alias>{containers}</character>";
                XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyLocationRename(
                    xml,
                    new LocationRenameRequest(workspaceId, 29, kind, target, "  Renamed exactly  "))).Root!;

                XElement location = root.Element(container)!.Element("location")!;
                Assert.AreEqual(target.ToString("D"), location.Element("guid")!.Value);
                Assert.AreEqual("  Renamed exactly  ", location.Element("name")!.Value);
                Assert.AreEqual($"{kind} notes", location.Element("notes")!.Value);
                Assert.AreEqual("Preserve me", root.Element("alias")!.Value);
                Assert.AreEqual(created.ToString(), root.Element("created")!.Value);
                foreach ((WorkspaceLocationKind otherKind, string otherContainer, _) in kinds)
                {
                    string expectedName = otherContainer == container
                        ? "  Renamed exactly  "
                        : $"{otherKind} old";
                    Assert.AreEqual(
                        expectedName,
                        root.Element(otherContainer)!.Element("location")!.Element("name")!.Value);
                }
            }
        }
    }

    [TestMethod]
    public void ApplyLocationRename_rejects_missing_duplicate_and_invalid_names()
    {
        CharacterWorkspaceId workspaceId = new("location-rename");
        Guid id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        const string duplicate = """
<character><gearlocations>
  <location><guid>11111111-1111-1111-1111-111111111111</guid><name>First</name><notes /></location>
  <location><guid>11111111-1111-1111-1111-111111111111</guid><name>Second</name><notes /></location>
</gearlocations></character>
""";

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyLocationRename(
            "<character><gearlocations /></character>",
            new LocationRenameRequest(workspaceId, 29, WorkspaceLocationKind.Gear, id, "New")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyLocationRename(
            duplicate,
            new LocationRenameRequest(workspaceId, 29, WorkspaceLocationKind.Gear, id, "New")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyLocationRename(
            "<character><gearlocations /></character>",
            new LocationRenameRequest(workspaceId, 29, WorkspaceLocationKind.Gear, id, string.Empty)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyLocationRename(
            "<character><gearlocations /></character>",
            new LocationRenameRequest(
                workspaceId,
                29,
                WorkspaceLocationKind.Gear,
                id,
                new string('x', LocationRenameRequest.MaximumNameLength + 1))));
    }

    [TestMethod]
    public void ApplyQuickAdd_supports_runtime_backed_aug_magic_matrix_and_advancement_kinds()
    {
        (WorkspaceQuickAddRequest Request, string[] RequiredMarkers)[] expectations =
        [
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.Drug,
                    Name: "Jazz",
                    Quantity: 2,
                    Source: "Core Rulebook p. 411"),
                ["<drugs>", "<drug>", "<name>Jazz</name>", "<qty>2</qty>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.Cyberware,
                    Name: "Wired Reflexes 2",
                    Category: "Bodyware",
                    Cost: "149000",
                    Rating: 2,
                    Grade: "Alpha",
                    Essence: "2.70",
                    Capacity: "n/a",
                    Location: "Body"),
                ["<cyberwares>", "<cyberware>", "<name>Wired Reflexes 2</name>", "<ess>2.70</ess>", "<grade>Alpha</grade>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.Spell,
                    Name: "Stunbolt",
                    Category: "Combat",
                    Type: "Mana",
                    Range: "LOS",
                    Duration: "Instant",
                    DrainValue: "F-3",
                    Source: "Core Rulebook p. 288"),
                ["<spells>", "<spell>", "<name>Stunbolt</name>", "<dv>F-3</dv>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.Power,
                    Name: "Improved Reflexes",
                    Rating: 1,
                    PointsPerLevel: 2.5m,
                    Source: "Core Rulebook p. 309"),
                ["<powers>", "<power>", "<name>Improved Reflexes</name>", "<pointsperlevel>2.5</pointsperlevel>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.ComplexForm,
                    Name: "Cleaner",
                    Target: "Persona",
                    Duration: "Sustained",
                    FadingValue: "Level 1",
                    Source: "Data Trails p. 178"),
                ["<complexforms>", "<complexform>", "<name>Cleaner</name>", "<fv>Level 1</fv>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.MatrixProgram,
                    Name: "Armor",
                    Slot: "Common",
                    Source: "Data Trails p. 60"),
                ["<aiprograms>", "<program>", "<name>Armor</name>", "<rating>Common</rating>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.InitiationGrade,
                    Name: "Masking",
                    Rating: 1,
                    Res: false),
                ["<initiationgrades>", "<initiationgrade>", "<grade>1</grade>", "<reward>Masking</reward>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.Spirit,
                    Name: "Watcher Spirit",
                    Force: 3,
                    Services: 2,
                    Bound: false),
                ["<spirits>", "<spirit>", "<name>Watcher Spirit</name>", "<force>3</force>", "<services>2</services>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.CritterPower,
                    Name: "Natural Weapon",
                    Type: "Passive",
                    Range: "Self",
                    Duration: "Always",
                    Rating: 1),
                ["<critterpowers>", "<critterpower>", "<name>Natural Weapon</name>", "<range>Self</range>", "<duration>Always</duration>"]
            )
        ];

        foreach ((WorkspaceQuickAddRequest request, string[] requiredMarkers) in expectations)
        {
            string xml = WorkspaceXmlMutationCatalog.ApplyQuickAdd("<character />", request);

            foreach (string marker in requiredMarkers)
            {
                StringAssert.Contains(xml, marker, $"Missing '{marker}' for kind '{request.Kind}'.");
            }

            string stableId = XDocument.Parse(xml).Descendants("guid").Single().Value;
            Assert.IsTrue(Guid.TryParse(stableId, out _), $"Quick-add kind '{request.Kind}' did not receive a stable GUID.");
        }
    }

    [TestMethod]
    public void ApplyQuickAdd_assigns_a_stable_guid_to_every_supported_collection_kind()
    {
        WorkspaceQuickAddRequest[] requests =
        [
            new(WorkspaceQuickAddKinds.Gear, "Gear"),
            new(WorkspaceQuickAddKinds.Weapon, "Weapon"),
            new(WorkspaceQuickAddKinds.Armor, "Armor"),
            new(WorkspaceQuickAddKinds.Skill, "Skill"),
            new(WorkspaceQuickAddKinds.Contact, "Contact"),
            new(WorkspaceQuickAddKinds.Pet, "Pet"),
            new(WorkspaceQuickAddKinds.Vehicle, "Vehicle"),
            new(WorkspaceQuickAddKinds.Quality, "Quality"),
            new(WorkspaceQuickAddKinds.Drug, "Drug"),
            new(WorkspaceQuickAddKinds.Cyberware, "Cyberware"),
            new(WorkspaceQuickAddKinds.Spell, "Spell"),
            new(WorkspaceQuickAddKinds.Power, "Power"),
            new(WorkspaceQuickAddKinds.ComplexForm, "Complex Form"),
            new(WorkspaceQuickAddKinds.MatrixProgram, "Program"),
            new(WorkspaceQuickAddKinds.InitiationGrade, "Reward"),
            new(WorkspaceQuickAddKinds.Spirit, "Spirit"),
            new(WorkspaceQuickAddKinds.CritterPower, "Critter Power")
        ];

        foreach (WorkspaceQuickAddRequest request in requests)
        {
            XDocument document = XDocument.Parse(
                WorkspaceXmlMutationCatalog.ApplyQuickAdd("<character />", request));
            XElement[] stableIds = document.Descendants("guid").ToArray();
            Assert.HasCount(1, stableIds, $"Quick-add kind '{request.Kind}' must emit exactly one stable ID.");
            Assert.IsTrue(
                Guid.TryParse(stableIds[0].Value, out _),
                $"Quick-add kind '{request.Kind}' emitted an invalid stable ID.");
        }
    }

    [TestMethod]
    public void ApplyCollectionMutation_edits_only_closed_fields_by_stable_id()
    {
        const string xml = """
<character>
  <alias>Preserve me</alias>
  <gears>
    <gear>
      <guid>gear-1</guid>
      <name>Renraku Sensei</name>
      <rating>2</rating>
      <qty>1</qty>
      <equipped>False</equipped>
      <wirelesson>False</wirelesson>
      <homenode>False</homenode>
    </gear>
  </gears>
</character>
""";
        WorkspaceCollectionItemTarget target = new(WorkspaceCollectionKind.Gear, "gear-1");

        string mutatedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionTextRequest(target, WorkspaceCollectionTextField.Notes, "Primary field kit"));
        mutatedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            mutatedXml,
            new WorkspaceSetCollectionTextRequest(target, WorkspaceCollectionTextField.CustomName, "Ghostline"));
        mutatedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            mutatedXml,
            new WorkspaceSetCollectionRatingRequest(target, 6));
        mutatedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            mutatedXml,
            new WorkspaceSetCollectionQuantityRequest(target, 2.5m));
        mutatedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            mutatedXml,
            new WorkspaceSetCollectionToggleRequest(target, WorkspaceCollectionToggleField.Equipped, true));
        mutatedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            mutatedXml,
            new WorkspaceSetCollectionToggleRequest(target, WorkspaceCollectionToggleField.WirelessEnabled, true));
        mutatedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            mutatedXml,
            new WorkspaceSetCollectionToggleRequest(target, WorkspaceCollectionToggleField.HomeNode, true));

        XElement root = XDocument.Parse(mutatedXml).Root!;
        XElement gear = root.Element("gears")!.Element("gear")!;
        Assert.AreEqual("gear-1", gear.Element("guid")?.Value);
        Assert.AreEqual("Primary field kit", gear.Element("notes")?.Value);
        Assert.AreEqual("Ghostline", gear.Element("extra")?.Value);
        Assert.AreEqual("6", gear.Element("rating")?.Value);
        Assert.AreEqual("2.5", gear.Element("qty")?.Value);
        Assert.AreEqual("True", gear.Element("equipped")?.Value);
        Assert.AreEqual("True", gear.Element("wirelesson")?.Value);
        Assert.AreEqual("True", gear.Element("homenode")?.Value);
        Assert.AreEqual("Preserve me", root.Element("alias")?.Value);
    }

    [TestMethod]
    public void ApplyCollectionMutation_updates_nested_notes_by_parent_and_child_stable_ids()
    {
        (string Xml, WorkspaceCollectionItemTarget Target, string ChildElement, string ChildId)[] cases =
        [
            (
                "<character><gears><gear><guid>gear-parent</guid><name>Parent</name><notes>Parent note</notes><children><gear><guid>gear-plugin</guid><name>Plugin</name><notes>Old</notes><children /></gear></children></gear></gears></character>",
                new WorkspaceCollectionItemTarget(
                    WorkspaceCollectionKind.Gear,
                    "gear-parent",
                    WorkspaceNestedCollectionKind.Gear,
                    "gear-plugin"),
                "gear",
                "gear-plugin"),
            (
                "<character><weapons><weapon><guid>weapon-parent</guid><name>Parent</name><notes>Parent note</notes><accessories><accessory><guid>weapon-accessory</guid><name>Accessory</name><notes>Old</notes></accessory></accessories></weapon></weapons></character>",
                new WorkspaceCollectionItemTarget(
                    WorkspaceCollectionKind.Weapon,
                    "weapon-parent",
                    WorkspaceNestedCollectionKind.WeaponAccessory,
                    "weapon-accessory"),
                "accessory",
                "weapon-accessory"),
            (
                "<character><armors><armor><guid>armor-parent</guid><name>Parent</name><notes>Parent note</notes><armormods><armormod><guid>armor-mod</guid><name>Mod</name><notes>Old</notes></armormod></armormods></armor></armors></character>",
                new WorkspaceCollectionItemTarget(
                    WorkspaceCollectionKind.Armor,
                    "armor-parent",
                    WorkspaceNestedCollectionKind.ArmorMod,
                    "armor-mod"),
                "armormod",
                "armor-mod")
        ];

        foreach ((string xml, WorkspaceCollectionItemTarget target, string childElement, string childId) in cases)
        {
            string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspaceSetCollectionTextRequest(
                    target,
                    WorkspaceCollectionTextField.Notes,
                    "Updated nested note"));

            XElement root = XDocument.Parse(mutated).Root!;
            XElement child = root.Descendants(childElement)
                .Single(item => item.Element("guid")?.Value == childId);
            Assert.AreEqual("Updated nested note", child.Element("notes")?.Value);
            Assert.AreEqual(
                "Parent note",
                root.Descendants().First(item => item.Element("guid")?.Value == target.ItemId)
                    .Element("notes")?.Value);
        }
    }

    [TestMethod]
    public void ApplyCollectionMutation_applies_a_multi_field_patch_atomically()
    {
        const string xml = """
<character>
  <gears>
    <gear>
      <guid>gear-atomic</guid>
      <name>Old name</name>
      <rating>1</rating>
      <qty>1</qty>
      <equipped>False</equipped>
    </gear>
  </gears>
</character>
""";
        WorkspaceCollectionItemTarget target = new(WorkspaceCollectionKind.Gear, "gear-atomic");

        string mutatedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(
                target,
                TextValues: new Dictionary<WorkspaceCollectionTextField, string?>
                {
                    [WorkspaceCollectionTextField.Name] = "Field Kit",
                    [WorkspaceCollectionTextField.Notes] = "One durable save"
                },
                Rating: 4,
                Quantity: 2.5m,
                ToggleValues: new Dictionary<WorkspaceCollectionToggleField, bool>
                {
                    [WorkspaceCollectionToggleField.Equipped] = true
                }));

        XElement gear = XDocument.Parse(mutatedXml).Descendants("gear").Single();
        Assert.AreEqual("Field Kit", gear.Element("name")?.Value);
        Assert.AreEqual("One durable save", gear.Element("notes")?.Value);
        Assert.AreEqual("4", gear.Element("rating")?.Value);
        Assert.AreEqual("2.5", gear.Element("qty")?.Value);
        Assert.AreEqual("True", gear.Element("equipped")?.Value);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspacePatchCollectionItemRequest(
                    target,
                    TextValues: new Dictionary<WorkspaceCollectionTextField, string?>
                    {
                        [WorkspaceCollectionTextField.Name] = "Would be partial"
                    },
                    Rating: 1001)));
        Assert.AreEqual("Old name", XDocument.Parse(xml).Descendants("gear").Single().Element("name")?.Value);
    }

    [TestMethod]
    public void ApplyCollectionMutation_updates_vehicle_damage_tracks_by_stable_id_against_current_bounds()
    {
        const string xml = """
<character>
  <created>True</created>
  <alias>Preserve me</alias>
  <vehicles>
    <vehicle><guid>vehicle-1</guid><name>Roadmaster</name><category>Groundcraft</category><body>5</body><pilot>3</pilot><physicalcmfilled>1</physicalcmfilled><matrixcmfilled>1</matrixcmfilled><mods /><gears><gear><guid>vehicle-gear</guid><name>Module</name><equipped>True</equipped><matrixcmbonus>1</matrixcmbonus><children /></gear></gears></vehicle>
    <vehicle><guid>vehicle-2</guid><name>Bulldog</name><category>Groundcraft</category><body>4</body><pilot>2</pilot><physicalcmfilled>2</physicalcmfilled><matrixcmfilled>2</matrixcmfilled><mods /></vehicle>
  </vehicles>
</character>
""";

        string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Vehicle, "vehicle-1"),
                TextValues: new Dictionary<WorkspaceCollectionTextField, string?>
                {
                    [WorkspaceCollectionTextField.Body] = "7"
                },
                VehiclePhysicalDamage: 16,
                VehicleMatrixDamage: 11));

        XElement root = XDocument.Parse(mutated).Root!;
        XElement selected = root.Descendants("vehicle").Single(vehicle => vehicle.Element("guid")?.Value == "vehicle-1");
        XElement other = root.Descendants("vehicle").Single(vehicle => vehicle.Element("guid")?.Value == "vehicle-2");
        Assert.AreEqual("7", selected.Element("body")?.Value);
        Assert.AreEqual("16", selected.Element("physicalcmfilled")?.Value);
        Assert.AreEqual("11", selected.Element("matrixcmfilled")?.Value);
        Assert.AreEqual("2", other.Element("physicalcmfilled")?.Value);
        Assert.AreEqual("2", other.Element("matrixcmfilled")?.Value);
        Assert.AreEqual("Preserve me", root.Element("alias")?.Value);
    }

    [TestMethod]
    public void ApplyCollectionMutation_rejects_vehicle_damage_without_exact_career_bounds()
    {
        const string creation = """
<character><created>False</created><vehicles><vehicle><guid>vehicle-1</guid><category>Groundcraft</category><body>5</body><pilot>3</pilot><physicalcmfilled>0</physicalcmfilled><matrixcmfilled>0</matrixcmfilled><mods /></vehicle></vehicles></character>
""";
        const string career = """
<character><created>True</created><vehicles><vehicle><guid>vehicle-1</guid><category>Groundcraft</category><body>5</body><pilot>3</pilot><physicalcmfilled>0</physicalcmfilled><matrixcmfilled>0</matrixcmfilled><mods /></vehicle></vehicles></character>
""";
        const string unknownModifier = """
<character><created>True</created><vehicles><vehicle><guid>vehicle-1</guid><category>Groundcraft</category><body>5</body><pilot>3</pilot><physicalcmfilled>0</physicalcmfilled><matrixcmfilled>0</matrixcmfilled><mods><mod><included>False</included><equipped>True</equipped><rating>1</rating><conditionmonitor>0</conditionmonitor></mod></mods></vehicle></vehicles></character>
""";
        const string overclocked = """
<character><created>True</created><improvements><improvement><improvementttype>Overclocker</improvementttype><enabled>1</enabled><addtorating>0</addtorating></improvement></improvements><vehicles><vehicle><guid>vehicle-1</guid><category>Groundcraft</category><body>5</body><pilot>2</pilot><physicalcmfilled>0</physicalcmfilled><matrixcmfilled>0</matrixcmfilled><overclocked>Device Rating</overclocked><mods /></vehicle></vehicles></character>
""";
        WorkspaceCollectionItemTarget target = new(WorkspaceCollectionKind.Vehicle, "vehicle-1");

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            creation,
            new WorkspacePatchCollectionItemRequest(target, VehiclePhysicalDamage: 1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            career,
            new WorkspacePatchCollectionItemRequest(target, VehiclePhysicalDamage: 16)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            career,
            new WorkspacePatchCollectionItemRequest(target, VehicleMatrixDamage: 11)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            unknownModifier,
            new WorkspacePatchCollectionItemRequest(target, VehiclePhysicalDamage: 1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            unknownModifier,
            new WorkspacePatchCollectionItemRequest(target, VehicleMatrixDamage: 1)));
        string overclockedMutation = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            overclocked,
            new WorkspacePatchCollectionItemRequest(target, VehicleMatrixDamage: 10));
        Assert.AreEqual("10", XDocument.Parse(overclockedMutation).Descendants("vehicle").Single()
            .Element("matrixcmfilled")?.Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            career,
            new WorkspacePatchCollectionItemRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "vehicle-1"),
                VehiclePhysicalDamage: 1)));
    }

    [TestMethod]
    public void ApplyCollectionMutation_uses_source_context_for_vehicle_and_cyberware_boundaries()
    {
        const string xml = """
<character>
  <created>True</created><alias>Preserve me</alias>
  <vehicles>
    <vehicle>
      <guid>vehicle-source</guid><name>Source-backed van</name><category>Groundcraft</category>
      <body>4</body><pilot>4</pilot><physicalcmfilled>1</physicalcmfilled><matrixcmfilled>1</matrixcmfilled>
      <mods><mod><sourceid>f89a112e-600a-4278-8731-9b14cf3737c9</sourceid><name>Gyro-Stabilization</name><included>False</included><equipped>True</equipped><rating>2</rating><conditionmonitor>1</conditionmonitor></mod></mods>
    </vehicle>
  </vehicles>
  <cyberwares>
    <cyberware><guid>cyber-source</guid><name>Source-backed implant</name><grade>Standard</grade><improvementsource>Cyberware</improvementsource><matrixcmfilled>1</matrixcmfilled></cyberware>
  </cyberwares>
</character>
""";
        var resolver = new FixedSourceDataResolver();
        WorkspaceCollectionItemTarget vehicle = new(WorkspaceCollectionKind.Vehicle, "vehicle-source");
        WorkspaceCollectionItemTarget cyberware = new(WorkspaceCollectionKind.Cyberware, "cyber-source");

        string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(
                vehicle,
                VehiclePhysicalDamage: 17,
                VehicleMatrixDamage: 14),
            resolver);
        mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            mutated,
            new WorkspacePatchCollectionItemRequest(cyberware, CyberwareMatrixDamage: 10),
            resolver);

        XElement root = XDocument.Parse(mutated).Root!;
        XElement selectedVehicle = root.Descendants("vehicle").Single();
        Assert.AreEqual("17", selectedVehicle.Element("physicalcmfilled")?.Value);
        Assert.AreEqual("14", selectedVehicle.Element("matrixcmfilled")?.Value);
        Assert.AreEqual("10", root.Descendants("cyberware").Single().Element("matrixcmfilled")?.Value);
        Assert.AreEqual("Preserve me", root.Element("alias")?.Value);

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(vehicle, VehiclePhysicalDamage: 18),
            resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(vehicle, VehicleMatrixDamage: 15),
            resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(cyberware, CyberwareMatrixDamage: 11),
            resolver));
    }

    [TestMethod]
    public void ApplyCollectionMutation_updates_nested_gear_matrix_damage_by_stable_id()
    {
        const string xml = """
<character>
  <created>True</created><alias>Preserve me</alias>
  <gears>
    <gear>
      <guid>gear-root</guid><name>Cyberdeck</name><rating>3</rating><devicerating>Rating</devicerating><matrixcmfilled>2</matrixcmfilled><matrixcmbonus>1</matrixcmbonus><equipped>True</equipped>
      <children>
        <gear><guid>gear-child</guid><name>Module</name><devicerating>2</devicerating><matrixcmfilled>1</matrixcmfilled><matrixcmbonus>2</matrixcmbonus><equipped>True</equipped><children /></gear>
        <gear><guid>gear-sibling</guid><name>Sibling</name><devicerating>1</devicerating><matrixcmfilled>3</matrixcmfilled><matrixcmbonus>0</matrixcmbonus><equipped>False</equipped><children /></gear>
      </children>
    </gear>
  </gears>
</character>
""";
        WorkspaceCollectionItemTarget child = new(
            WorkspaceCollectionKind.Gear,
            "gear-root",
            WorkspaceNestedCollectionKind.Gear,
            "gear-child");

        string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(child, GearMatrixDamage: 11));

        XElement root = XDocument.Parse(mutated).Root!;
        Assert.AreEqual("11", root.Descendants("gear").Single(item => item.Element("guid")?.Value == "gear-child")
            .Element("matrixcmfilled")?.Value);
        Assert.AreEqual("2", root.Descendants("gear").Single(item => item.Element("guid")?.Value == "gear-root")
            .Element("matrixcmfilled")?.Value);
        Assert.AreEqual("3", root.Descendants("gear").Single(item => item.Element("guid")?.Value == "gear-sibling")
            .Element("matrixcmfilled")?.Value);
        Assert.AreEqual("Preserve me", root.Element("alias")?.Value);
    }

    [TestMethod]
    public void ApplyCollectionMutation_rejects_gear_matrix_damage_without_exact_career_bounds()
    {
        const string creation = """
<character><created>False</created><gears><gear><guid>gear-1</guid><name>Deck</name><devicerating>2</devicerating><matrixcmfilled>0</matrixcmfilled><matrixcmbonus>0</matrixcmbonus><children /></gear></gears></character>
""";
        const string career = """
<character><created>True</created><gears><gear><guid>gear-1</guid><name>Deck</name><devicerating>2</devicerating><matrixcmfilled>0</matrixcmfilled><matrixcmbonus>0</matrixcmbonus><children /></gear></gears></character>
""";
        const string overclocked = """
<character><created>True</created><improvements><improvement><improvementttype>Overclocker</improvementttype><enabled>1</enabled><addtorating>0</addtorating></improvement></improvements><gears><gear><guid>gear-1</guid><name>Deck</name><devicerating>2</devicerating><overclocked>Device Rating</overclocked><matrixcmfilled>0</matrixcmfilled><matrixcmbonus>0</matrixcmbonus><children /></gear></gears></character>
""";
        const string livingPersona = """
<character><created>True</created><attributes><attribute><name>RES</name><totalvalue>3</totalvalue></attribute></attributes><improvements><improvement><improvedname>+2</improvedname><improvementttype>LivingPersonaDeviceRating</improvementttype><enabled>1</enabled><addtorating>0</addtorating></improvement><improvement><improvedname>+1</improvedname><improvementttype>LivingPersonaMatrixCM</improvementttype><enabled>1</enabled><addtorating>0</addtorating></improvement></improvements><gears><gear><guid>gear-1</guid><name>Living Persona</name><rating>3</rating><devicerating>{RES}</devicerating><canformpersona>Self</canformpersona><matrixcmfilled>0</matrixcmfilled><matrixcmbonus>0</matrixcmbonus><children /></gear></gears></character>
""";
        const string selectedLivingPersona = """
<character><created>True</created><improvements><improvement><unique>precedence0</unique><improvedname>+2</improvedname><val>6</val><improvementttype>LivingPersonaDeviceRating</improvementttype><enabled>1</enabled><addtorating>0</addtorating></improvement><improvement><unique>precedence0</unique><improvedname>+2</improvedname><val>4</val><improvementttype>LivingPersonaDeviceRating</improvementttype><enabled>1</enabled><addtorating>0</addtorating></improvement><improvement><custom>True</custom><improvedname>+1</improvedname><val>0</val><improvementttype>LivingPersonaDeviceRating</improvementttype><enabled>1</enabled><addtorating>0</addtorating></improvement><improvement><improvedname>+2</improvedname><val>0</val><improvementttype>LivingPersonaMatrixCM</improvementttype><enabled>1</enabled><addtorating>0</addtorating></improvement><improvement><custom>True</custom><unique>boxes</unique><improvedname>+2</improvedname><val>1</val><improvementttype>LivingPersonaMatrixCM</improvementttype><enabled>1</enabled><addtorating>0</addtorating></improvement><improvement><custom>True</custom><unique>boxes</unique><improvedname>+2</improvedname><val>3</val><improvementttype>LivingPersonaMatrixCM</improvementttype><enabled>1</enabled><addtorating>0</addtorating></improvement></improvements><gears><gear><guid>gear-1</guid><name>Living Persona</name><rating>3</rating><devicerating>Rating</devicerating><canformpersona>Self</canformpersona><matrixcmfilled>0</matrixcmfilled><matrixcmbonus>0</matrixcmbonus><children /></gear></gears></character>
""";
        const string malformedLivingPersona = """
<character><created>True</created><improvements><improvement><unique>precedence0</unique><improvedname>+2</improvedname><val>invalid</val><improvementttype>LivingPersonaDeviceRating</improvementttype><enabled>1</enabled><addtorating>0</addtorating></improvement></improvements><gears><gear><guid>gear-1</guid><name>Living Persona</name><rating>3</rating><devicerating>Rating</devicerating><canformpersona>Self</canformpersona><matrixcmfilled>0</matrixcmfilled><matrixcmbonus>0</matrixcmbonus><children /></gear></gears></character>
""";
        WorkspaceCollectionItemTarget target = new(WorkspaceCollectionKind.Gear, "gear-1");

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            creation,
            new WorkspacePatchCollectionItemRequest(target, GearMatrixDamage: 1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            career,
            new WorkspacePatchCollectionItemRequest(target, GearMatrixDamage: 10)));
        string overclockedMutation = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            overclocked,
            new WorkspacePatchCollectionItemRequest(target, GearMatrixDamage: 10));
        Assert.AreEqual("10", XDocument.Parse(overclockedMutation).Descendants("gear").Single()
            .Element("matrixcmfilled")?.Value);
        string livingPersonaMutation = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            livingPersona,
            new WorkspacePatchCollectionItemRequest(target, GearMatrixDamage: 12));
        Assert.AreEqual("12", XDocument.Parse(livingPersonaMutation).Descendants("gear").Single()
            .Element("matrixcmfilled")?.Value);
        string selectedLivingPersonaMutation = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            selectedLivingPersona,
            new WorkspacePatchCollectionItemRequest(target, GearMatrixDamage: 15));
        Assert.AreEqual("15", XDocument.Parse(selectedLivingPersonaMutation).Descendants("gear").Single()
            .Element("matrixcmfilled")?.Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            selectedLivingPersona,
            new WorkspacePatchCollectionItemRequest(target, GearMatrixDamage: 16)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            malformedLivingPersona,
            new WorkspacePatchCollectionItemRequest(target, GearMatrixDamage: 1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            career,
            new WorkspacePatchCollectionItemRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Vehicle, "gear-1"),
                GearMatrixDamage: 1)));
    }

    [TestMethod]
    public void ApplyCollectionMutation_updates_and_validates_armor_matrix_damage_by_stable_id()
    {
        const string xml = """
<character>
  <created>True</created><alias>Preserve me</alias>
  <armors>
    <armor><guid>armor-1</guid><name>Armor jacket</name><devicerating>3</devicerating><matrixcmfilled>1</matrixcmfilled><matrixcmbonus>1</matrixcmbonus><gears><gear><guid>armor-gear</guid><name>Module</name><equipped>True</equipped><matrixcmbonus>2</matrixcmbonus><children /></gear></gears></armor>
    <armor><guid>armor-2</guid><name>Coat</name><devicerating>2</devicerating><matrixcmfilled>2</matrixcmfilled><matrixcmbonus>0</matrixcmbonus><children /></armor>
  </armors>
</character>
""";
        WorkspaceCollectionItemTarget target = new(WorkspaceCollectionKind.Armor, "armor-1");

        string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(target, ArmorMatrixDamage: 13));

        XElement root = XDocument.Parse(mutated).Root!;
        Assert.AreEqual("13", root.Descendants("armor").Single(item => item.Element("guid")?.Value == "armor-1")
            .Element("matrixcmfilled")?.Value);
        Assert.AreEqual("2", root.Descendants("armor").Single(item => item.Element("guid")?.Value == "armor-2")
            .Element("matrixcmfilled")?.Value);
        Assert.AreEqual("Preserve me", root.Element("alias")?.Value);

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml.Replace("<created>True</created>", "<created>False</created>", StringComparison.Ordinal),
            new WorkspacePatchCollectionItemRequest(target, ArmorMatrixDamage: 1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(target, ArmorMatrixDamage: 14)));
        string inactiveArmorOverclock = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml.Replace("<matrixcmfilled>1</matrixcmfilled>", "<overclocked>Device Rating</overclocked><matrixcmfilled>1</matrixcmfilled>", StringComparison.Ordinal),
            new WorkspacePatchCollectionItemRequest(target, ArmorMatrixDamage: 1));
        Assert.AreEqual("1", XDocument.Parse(inactiveArmorOverclock).Descendants("armor").First()
            .Element("matrixcmfilled")?.Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml.Replace("<devicerating>3</devicerating>", "<devicerating>Rating + 1</devicerating>", StringComparison.Ordinal),
            new WorkspacePatchCollectionItemRequest(target, ArmorMatrixDamage: 1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "armor-1"),
                ArmorMatrixDamage: 1)));
    }

    [TestMethod]
    public void ApplyCollectionMutation_updates_and_validates_weapon_matrix_damage_by_stable_id()
    {
        const string xml = """
<character>
  <created>True</created><alias>Preserve me</alias>
  <weapons>
    <weapon><guid>weapon-1</guid><name>Smartgun</name><rating>3</rating><devicerating>{Rating}</devicerating><matrixcmfilled>1</matrixcmfilled></weapon>
    <weapon><guid>weapon-2</guid><name>Holdout</name><matrixcmfilled>2</matrixcmfilled></weapon>
  </weapons>
</character>
""";
        WorkspaceCollectionItemTarget target = new(WorkspaceCollectionKind.Weapon, "weapon-1");

        string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(target, WeaponMatrixDamage: 10));

        XElement root = XDocument.Parse(mutated).Root!;
        Assert.AreEqual("10", root.Descendants("weapon").Single(item => item.Element("guid")?.Value == "weapon-1")
            .Element("matrixcmfilled")?.Value);
        Assert.AreEqual("2", root.Descendants("weapon").Single(item => item.Element("guid")?.Value == "weapon-2")
            .Element("matrixcmfilled")?.Value);
        Assert.AreEqual("Preserve me", root.Element("alias")?.Value);

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml.Replace("<created>True</created>", "<created>False</created>", StringComparison.Ordinal),
            new WorkspacePatchCollectionItemRequest(target, WeaponMatrixDamage: 1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(target, WeaponMatrixDamage: 11)));
        string inactiveWeaponOverclock = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml.Replace("<matrixcmfilled>1</matrixcmfilled>", "<overclocked>Device Rating</overclocked><matrixcmfilled>1</matrixcmfilled>", StringComparison.Ordinal),
            new WorkspacePatchCollectionItemRequest(target, WeaponMatrixDamage: 1));
        Assert.AreEqual("1", XDocument.Parse(inactiveWeaponOverclock).Descendants("weapon").First()
            .Element("matrixcmfilled")?.Value);
        string staleParent = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml.Replace("<matrixcmfilled>1</matrixcmfilled>", "<parentid>gear-parent</parentid><matrixcmfilled>1</matrixcmfilled>", StringComparison.Ordinal),
            new WorkspacePatchCollectionItemRequest(target, WeaponMatrixDamage: 10));
        Assert.AreEqual("10", XDocument.Parse(staleParent).Descendants("weapon").First()
            .Element("matrixcmfilled")?.Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Armor, "weapon-1"),
                WeaponMatrixDamage: 1)));
    }

    [TestMethod]
    public void ApplyCollectionMutation_writes_weapon_matrix_damage_to_exact_saved_parent_owner()
    {
        const string xml = """
<character>
  <created>True</created>
  <gears>
    <gear><guid>gear-parent</guid><name>Deck</name><devicerating>4</devicerating><matrixcmfilled>2</matrixcmfilled><matrixcmbonus>1</matrixcmbonus><children><gear><guid>gear-module</guid><name>Module</name><equipped>True</equipped><matrixcmbonus>2</matrixcmbonus><children /></gear></children></gear>
    <gear><guid>duplicate-parent</guid><name>Duplicate one</name><devicerating>2</devicerating><matrixcmfilled>2</matrixcmfilled><children /></gear>
    <gear><guid>duplicate-parent</guid><name>Duplicate two</name><devicerating>3</devicerating><matrixcmfilled>3</matrixcmfilled><children /></gear>
  </gears>
  <weapons>
    <weapon><guid>weapon-child</guid><name>Gear child</name><parentid>gear-parent</parentid><matrixcmfilled>1</matrixcmfilled></weapon>
    <weapon><guid>weapon-parent</guid><name>Weapon parent</name><devicerating>6</devicerating><matrixcmfilled>4</matrixcmfilled></weapon>
    <weapon><guid>weapon-chain</guid><name>Weapon child</name><parentid>weapon-parent</parentid><matrixcmfilled>0</matrixcmfilled></weapon>
    <weapon><guid>weapon-stale</guid><name>Stale child</name><parentid>missing-parent</parentid><devicerating>3</devicerating><matrixcmfilled>2</matrixcmfilled></weapon>
    <weapon><guid>weapon-duplicate</guid><name>Ambiguous child</name><parentid>duplicate-parent</parentid><matrixcmfilled>1</matrixcmfilled></weapon>
    <weapon><guid>weapon-cycle-a</guid><name>Cycle A</name><parentid>weapon-cycle-b</parentid><matrixcmfilled>1</matrixcmfilled></weapon>
    <weapon><guid>weapon-cycle-b</guid><name>Cycle B</name><parentid>weapon-cycle-a</parentid><matrixcmfilled>2</matrixcmfilled></weapon>
  </weapons>
</character>
""";

        string gearMutated = Mutate("weapon-child", 13);
        XElement gearRoot = XDocument.Parse(gearMutated).Root!;
        Assert.AreEqual("13", gearRoot.Descendants("gear")
            .Single(item => item.Element("guid")?.Value == "gear-parent")
            .Element("matrixcmfilled")?.Value);
        Assert.AreEqual("1", gearRoot.Descendants("weapon")
            .Single(item => item.Element("guid")?.Value == "weapon-child")
            .Element("matrixcmfilled")?.Value);

        string chainMutated = Mutate("weapon-chain", 11);
        XElement chainRoot = XDocument.Parse(chainMutated).Root!;
        Assert.AreEqual("11", chainRoot.Descendants("weapon")
            .Single(item => item.Element("guid")?.Value == "weapon-parent")
            .Element("matrixcmfilled")?.Value);
        Assert.AreEqual("0", chainRoot.Descendants("weapon")
            .Single(item => item.Element("guid")?.Value == "weapon-chain")
            .Element("matrixcmfilled")?.Value);

        string staleMutated = Mutate("weapon-stale", 10);
        Assert.AreEqual("10", XDocument.Parse(staleMutated).Descendants("weapon")
            .Single(item => item.Element("guid")?.Value == "weapon-stale")
            .Element("matrixcmfilled")?.Value);

        Assert.ThrowsExactly<InvalidOperationException>(() => Mutate("weapon-child", 14));
        Assert.ThrowsExactly<InvalidOperationException>(() => Mutate("weapon-duplicate", 1));
        Assert.ThrowsExactly<InvalidOperationException>(() => Mutate("weapon-cycle-a", 1));

        string Mutate(string guid, int damage)
            => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspacePatchCollectionItemRequest(
                    new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Weapon, guid),
                    WeaponMatrixDamage: damage));
    }

    [TestMethod]
    public void ApplyCollectionMutation_updates_and_validates_recursive_cyberware_matrix_damage()
    {
        const string xml = """
<character>
  <created>True</created><alias>Preserve me</alias>
  <cyberwares>
    <cyberware>
      <guid>cyber-root</guid><name>Implanted deck</name><rating>3</rating><devicerating>Rating</devicerating><matrixcmfilled>1</matrixcmfilled><matrixcmbonus>99</matrixcmbonus>
      <gears><gear><guid>root-gear</guid><name>Module</name><equipped>True</equipped><matrixcmbonus>2</matrixcmbonus><children /></gear></gears>
      <children>
        <cyberware><guid>cyber-child</guid><name>Plugin</name><devicerating>2</devicerating><matrixcmfilled>1</matrixcmfilled><gears><gear><guid>child-gear</guid><name>Chip</name><equipped>True</equipped><matrixcmbonus>1</matrixcmbonus><children /></gear></gears></cyberware>
        <cyberware><guid>cyber-sibling</guid><name>Sibling</name><devicerating>2</devicerating><matrixcmfilled>2</matrixcmfilled></cyberware>
      </children>
    </cyberware>
  </cyberwares>
</character>
""";
        WorkspaceCollectionItemTarget rootTarget = new(WorkspaceCollectionKind.Cyberware, "cyber-root");
        WorkspaceCollectionItemTarget childTarget = new(
            WorkspaceCollectionKind.Cyberware,
            "cyber-root",
            WorkspaceNestedCollectionKind.CyberwarePlugin,
            "cyber-child");

        string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(rootTarget, CyberwareMatrixDamage: 13));
        mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            mutated,
            new WorkspacePatchCollectionItemRequest(childTarget, CyberwareMatrixDamage: 10));

        XElement root = XDocument.Parse(mutated).Root!;
        Assert.AreEqual("13", root.Descendants("cyberware").Single(item => item.Element("guid")?.Value == "cyber-root")
            .Element("matrixcmfilled")?.Value);
        Assert.AreEqual("10", root.Descendants("cyberware").Single(item => item.Element("guid")?.Value == "cyber-child")
            .Element("matrixcmfilled")?.Value);
        Assert.AreEqual("2", root.Descendants("cyberware").Single(item => item.Element("guid")?.Value == "cyber-sibling")
            .Element("matrixcmfilled")?.Value);
        Assert.AreEqual("Preserve me", root.Element("alias")?.Value);

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml.Replace("<created>True</created>", "<created>False</created>", StringComparison.Ordinal),
            new WorkspacePatchCollectionItemRequest(childTarget, CyberwareMatrixDamage: 1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(childTarget, CyberwareMatrixDamage: 11)));
        string inactiveCyberwareOverclock = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml.Replace("<devicerating>2</devicerating><matrixcmfilled>1</matrixcmfilled>", "<devicerating>2</devicerating><overclocked>Device Rating</overclocked><matrixcmfilled>1</matrixcmfilled>", StringComparison.Ordinal),
            new WorkspacePatchCollectionItemRequest(childTarget, CyberwareMatrixDamage: 1));
        Assert.AreEqual("1", XDocument.Parse(inactiveCyberwareOverclock).Descendants("cyberware")
            .Single(item => item.Element("guid")?.Value == "cyber-child").Element("matrixcmfilled")?.Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml.Replace("<devicerating>2</devicerating><matrixcmfilled>1</matrixcmfilled>", "<grade>Standard</grade><matrixcmfilled>1</matrixcmfilled>", StringComparison.Ordinal),
            new WorkspacePatchCollectionItemRequest(childTarget, CyberwareMatrixDamage: 1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "cyber-root"),
                CyberwareMatrixDamage: 1)));
    }

    [TestMethod]
    public void ApplyCollectionMutation_rejects_an_empty_patch()
    {
        const string xml = """
<character><gears><gear><guid>gear-1</guid><name>Kit</name></gear></gears></character>
""";

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspacePatchCollectionItemRequest(
                    new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "gear-1"))));
    }

    [TestMethod]
    public void ApplyCollectionMutation_reorders_and_deletes_without_moving_unrelated_nodes()
    {
        const string xml = """
<character>
  <gears>
    <gear><guid>gear-1</guid><name>One</name></gear>
    <marker>keep-position</marker>
    <gear><guid>gear-2</guid><name>Two</name></gear>
    <gear><guid>gear-3</guid><name>Three</name></gear>
  </gears>
  <qualities><quality><guid>quality-1</guid><name>Focused</name></quality></qualities>
</character>
""";

        string reorderedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceMoveCollectionItemRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "gear-3"),
                TargetIndex: 0));
        XDocument reordered = XDocument.Parse(reorderedXml);
        XElement gears = reordered.Root!.Element("gears")!;
        CollectionAssert.AreEqual(
            new[] { "gear-3", "gear-1", "gear-2" },
            gears.Elements("gear").Select(element => element.Element("guid")!.Value).ToArray());
        CollectionAssert.AreEqual(
            new[] { "gear", "marker", "gear", "gear" },
            gears.Elements().Select(element => element.Name.LocalName).ToArray());

        string deletedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            reorderedXml,
            new WorkspaceDeleteCollectionItemRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "gear-1")));
        XDocument deleted = XDocument.Parse(deletedXml);
        CollectionAssert.AreEqual(
            new[] { "gear-3", "gear-2" },
            deleted.Root!.Element("gears")!.Elements("gear")
                .Select(element => element.Element("guid")!.Value)
                .ToArray());
        Assert.AreEqual("keep-position", deleted.Descendants("marker").Single().Value);
        Assert.AreEqual("Focused", deleted.Descendants("quality").Single().Element("name")?.Value);
    }

    [TestMethod]
    public void ApplyCollectionMutation_adds_edits_and_removes_typed_nested_items()
    {
        const string xml = """
<character>
  <gears>
    <gear>
      <guid>parent-gear</guid>
      <name>Bug Scanner</name>
      <notes>Parent note</notes>
      <children>
        <gear><guid>existing-child</guid><name>Existing</name></gear>
      </children>
    </gear>
  </gears>
</character>
""";
        WorkspaceCollectionItemTarget parent = new(WorkspaceCollectionKind.Gear, "parent-gear");
        string addedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceAddNestedCollectionItemRequest(
                parent,
                WorkspaceNestedCollectionKind.Gear,
                new WorkspaceNestedItemDraft(
                    Name: "Tag Eraser",
                    Category: "Electronics",
                    Notes: "Nested note",
                    Rating: 3,
                    Quantity: 2m,
                    Equipped: true,
                    WirelessEnabled: false)));
        XDocument added = XDocument.Parse(addedXml);
        XElement addedChild = added.Descendants("gear")
            .Single(element => element.Element("name")?.Value == "Tag Eraser");
        string childId = addedChild.Element("guid")!.Value;
        Assert.IsTrue(Guid.TryParse(childId, out _));

        WorkspaceCollectionItemTarget child = new(
            WorkspaceCollectionKind.Gear,
            "parent-gear",
            WorkspaceNestedCollectionKind.Gear,
            childId);
        string editedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            addedXml,
            new WorkspaceSetCollectionTextRequest(child, WorkspaceCollectionTextField.Name, "Tag Eraser Mk II"));
        editedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            editedXml,
            new WorkspaceSetCollectionRatingRequest(child, 4));
        editedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            editedXml,
            new WorkspaceSetCollectionQuantityRequest(child, 3m));
        editedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            editedXml,
            new WorkspaceSetCollectionToggleRequest(child, WorkspaceCollectionToggleField.WirelessEnabled, true));

        XElement editedChild = XDocument.Parse(editedXml).Descendants("gear")
            .Single(element => element.Element("guid")?.Value == childId);
        Assert.AreEqual("Tag Eraser Mk II", editedChild.Element("name")?.Value);
        Assert.AreEqual("4", editedChild.Element("rating")?.Value);
        Assert.AreEqual("3", editedChild.Element("qty")?.Value);
        Assert.AreEqual("True", editedChild.Element("wirelesson")?.Value);

        string deletedXml = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            editedXml,
            new WorkspaceDeleteCollectionItemRequest(child));
        XDocument deleted = XDocument.Parse(deletedXml);
        Assert.IsFalse(deleted.Descendants("guid").Any(element => element.Value == childId));
        Assert.AreEqual("Existing", deleted.Descendants("gear")
            .Single(element => element.Element("guid")?.Value == "existing-child")
            .Element("name")?.Value);
        Assert.AreEqual("Parent note", deleted.Descendants("gear")
            .Single(element => element.Element("guid")?.Value == "parent-gear")
            .Element("notes")?.Value);
    }

    [TestMethod]
    public void ApplyCollectionMutation_resolves_nested_cyberware_parent_by_stable_id_at_any_depth()
    {
        const string xml = """
<character>
  <cyberwares>
    <cyberware>
      <guid>root</guid><name>Root</name>
      <children>
        <cyberware>
          <guid>parent</guid><name>Parent</name>
          <children><cyberware><guid>child</guid><name>Child</name><rating>1</rating></cyberware></children>
        </cyberware>
      </children>
    </cyberware>
  </cyberwares>
</character>
""";
        WorkspaceCollectionItemTarget child = new(
            WorkspaceCollectionKind.Cyberware,
            "parent",
            WorkspaceNestedCollectionKind.CyberwarePlugin,
            "child");

        string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(
                child,
                TextValues: new Dictionary<WorkspaceCollectionTextField, string?>
                {
                    [WorkspaceCollectionTextField.Name] = "Nested child"
                },
                Rating: 3));

        XElement item = XDocument.Parse(mutated).Descendants("cyberware")
            .Single(candidate => candidate.Element("guid")?.Value == "child");
        Assert.AreEqual("Nested child", item.Element("name")?.Value);
        Assert.AreEqual("3", item.Element("rating")?.Value);
    }

    [TestMethod]
    public void ApplyCollectionMutation_rejects_names_duplicates_unknown_fields_and_invalid_bounds()
    {
        const string xml = """
<character>
  <gears>
    <gear><guid>duplicate</guid><name>Commlink</name><qty>1</qty></gear>
    <gear><guid>duplicate</guid><name>Second copy</name><qty>1</qty></gear>
    <gear><guid>unique</guid><name>Unique</name><qty>1</qty></gear>
  </gears>
  <weapons><weapon><guid>weapon-1</guid><name>Pistol</name></weapon></weapons>
</character>
""";

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspaceSetCollectionTextRequest(
                    new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "Commlink"),
                    WorkspaceCollectionTextField.Notes,
                    "Name-only selectors are forbidden")));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspaceDeleteCollectionItemRequest(
                    new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "duplicate"))));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspaceSetCollectionTextRequest(
                    new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "unique"),
                    WorkspaceCollectionTextField.Damage,
                    "10P")));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspaceSetCollectionTextRequest(
                    new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "unique"),
                    (WorkspaceCollectionTextField)999,
                    "Unknown field")));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspaceSetCollectionQuantityRequest(
                    new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Weapon, "weapon-1"),
                    2m)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspaceSetCollectionRatingRequest(
                    new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "unique"),
                    1001)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspaceSetCollectionQuantityRequest(
                    new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "unique"),
                    0m)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspaceDeleteCollectionItemRequest(
                    new WorkspaceCollectionItemTarget((WorkspaceCollectionKind)999, "unique"))));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspaceAddNestedCollectionItemRequest(
                    new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Weapon, "weapon-1"),
                    WorkspaceNestedCollectionKind.ArmorMod,
                    new WorkspaceNestedItemDraft("Illegal child"))));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new UnsupportedCollectionMutationRequest(
                    new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, "unique"))));
    }

    private sealed record UnsupportedCollectionMutationRequest(WorkspaceCollectionItemTarget Target)
        : WorkspaceCollectionMutationRequest(Target);

    [TestMethod]
    public void ApplyAttributeEdit_updates_attribute_buckets_and_totalvalue()
    {
        const string xml = """
<character>
  <attributes>
    <attribute>
      <name>Body</name>
      <base>3</base>
      <karma>1</karma>
      <value>3</value>
      <totalvalue>4</totalvalue>
      <metatypemin>1</metatypemin>
      <metatypemax>6</metatypemax>
      <metatypeaugmax>9</metatypeaugmax>
    </attribute>
  </attributes>
</character>
""";

        string baseMutatedXml = WorkspaceXmlMutationCatalog.ApplyAttributeEdit(
            xml,
            new AttributeEditRequest("Body", "base", 5));
        StringAssert.Contains(baseMutatedXml, "<base>5</base>");
        StringAssert.Contains(baseMutatedXml, "<karma>1</karma>");
        StringAssert.Contains(baseMutatedXml, "<value>5</value>");
        StringAssert.Contains(baseMutatedXml, "<totalvalue>6</totalvalue>");

        string karmaMutatedXml = WorkspaceXmlMutationCatalog.ApplyAttributeEdit(
            baseMutatedXml,
            new AttributeEditRequest("Body", "karma", 9));
        StringAssert.Contains(karmaMutatedXml, "<base>5</base>");
        StringAssert.Contains(karmaMutatedXml, "<karma>4</karma>");
        StringAssert.Contains(karmaMutatedXml, "<totalvalue>9</totalvalue>");
    }

    [TestMethod]
    public void ApplyOriginDossierEdit_updates_all_editable_profile_fields_and_preserves_unrelated_state()
    {
        const string xml = """
<character>
  <name>Old Name</name>
  <alias>Old Alias</alias>
  <playername>Old Player</playername>
  <metatype>Human</metatype>
  <created>True</created>
</character>
""";

        string mutatedXml = WorkspaceXmlMutationCatalog.ApplyOriginDossierEdit(
            xml,
            new OriginDossierEditRequest(
                Name: "Rin & Vale",
                Alias: "Latchkey",
                PlayerName: "Tibor",
                Sex: "Non-binary",
                Age: "29",
                Height: "178 cm",
                Weight: "71 kg",
                Hair: "Black",
                Eyes: "Hazel",
                Skin: "Olive",
                Concept: "Infiltration specialist",
                Description: "Quiet under pressure.",
                Background: "Former corporate security."));

        XElement root = XDocument.Parse(mutatedXml).Root!;
        (string Element, string Expected)[] expectations =
        [
            ("name", "Rin & Vale"),
            ("alias", "Latchkey"),
            ("playername", "Tibor"),
            ("sex", "Non-binary"),
            ("age", "29"),
            ("height", "178 cm"),
            ("weight", "71 kg"),
            ("hair", "Black"),
            ("eyes", "Hazel"),
            ("skin", "Olive"),
            ("concept", "Infiltration specialist"),
            ("description", "Quiet under pressure."),
            ("background", "Former corporate security.")
        ];

        foreach ((string element, string expected) in expectations)
        {
            Assert.AreEqual(expected, root.Element(element)?.Value, $"Profile field '{element}' did not persist.");
        }

        Assert.AreEqual("Human", root.Element("metatype")?.Value);
        Assert.AreEqual("True", root.Element("created")?.Value);
    }

    [TestMethod]
    public void ApplyCollectionMutation_persists_all_direct_contact_values_atomically()
    {
        const string xml = """
<character>
  <created>False</created>
  <contacts>
    <contact>
      <guid>contact-1</guid><name>Old name</name><role>Old role</role><location>Old place</location>
      <connection>2</connection><loyalty>2</loyalty><group>False</group><free>False</free>
      <family>False</family><blackmail>False</blackmail><type>Contact</type>
    </contact>
  </contacts>
</character>
""";
        WorkspaceCollectionItemTarget target = new(WorkspaceCollectionKind.Contact, "contact-1");

        string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(
                target,
                TextValues: new Dictionary<WorkspaceCollectionTextField, string?>
                {
                    [WorkspaceCollectionTextField.Name] = "Ms. Johnson",
                    [WorkspaceCollectionTextField.Role] = "Fixer",
                    [WorkspaceCollectionTextField.Location] = "Vienna",
                    [WorkspaceCollectionTextField.Metatype] = "Elf",
                    [WorkspaceCollectionTextField.Gender] = "Female",
                    [WorkspaceCollectionTextField.Age] = "42",
                    [WorkspaceCollectionTextField.ContactType] = "Professional",
                    [WorkspaceCollectionTextField.PreferredPayment] = "Credstick",
                    [WorkspaceCollectionTextField.HobbiesVice] = "Urban exploration",
                    [WorkspaceCollectionTextField.PersonalLife] = "Private",
                    [WorkspaceCollectionTextField.GroupName] = "Night Market",
                    [WorkspaceCollectionTextField.Notes] = "Keep it discreet."
                },
                ToggleValues: new Dictionary<WorkspaceCollectionToggleField, bool>
                {
                    [WorkspaceCollectionToggleField.Group] = false,
                    [WorkspaceCollectionToggleField.Free] = true,
                    [WorkspaceCollectionToggleField.Family] = true,
                    [WorkspaceCollectionToggleField.Blackmail] = true
                },
                ContactConnection: 6,
                ContactLoyalty: 5));

        XElement contact = XDocument.Parse(mutated).Root!.Element("contacts")!.Element("contact")!;
        (string Element, string Expected)[] values =
        [
            ("name", "Ms. Johnson"),
            ("role", "Fixer"),
            ("location", "Vienna"),
            ("metatype", "Elf"),
            ("gender", "Female"),
            ("age", "42"),
            ("contacttype", "Professional"),
            ("preferredpayment", "Credstick"),
            ("hobbiesvice", "Urban exploration"),
            ("personallife", "Private"),
            ("groupname", "Night Market"),
            ("notes", "Keep it discreet."),
            ("group", "False"),
            ("free", "True"),
            ("family", "True"),
            ("blackmail", "True"),
            ("connection", "6"),
            ("loyalty", "5")
        ];
        foreach ((string element, string expected) in values)
        {
            Assert.AreEqual(expected, contact.Element(element)?.Value, $"Contact field '{element}' did not persist.");
        }

        string grouped = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            mutated,
            new WorkspaceSetCollectionToggleRequest(target, WorkspaceCollectionToggleField.Group, true));
        Assert.AreEqual(
            "True",
            XDocument.Parse(grouped).Root!.Element("contacts")!.Element("contact")!.Element("group")!.Value);
    }

    [TestMethod]
    public void ApplyCollectionMutation_enforces_contact_career_link_improvement_enemy_and_readonly_rules()
    {
        const string xml = """
<character>
  <created>True</created>
  <improvements>
    <improvement><improvementttype>FriendsInHighPlaces</improvementttype><enabled>1</enabled><condition>career</condition></improvement>
    <improvement><improvementttype>ContactForceGroup</improvementttype><improvedname>linked</improvedname><enabled>1</enabled></improvement>
    <improvement><improvementttype>ContactMakeFree</improvementttype><improvedname>linked</improvedname><enabled>1</enabled></improvement>
    <improvement><improvementttype>ContactForcedLoyalty</improvementttype><improvedname>linked</improvedname><val>4</val><enabled>1</enabled></improvement>
  </improvements>
  <contacts>
    <contact><guid>linked</guid><name>Linked</name><role>Fixer</role><file>linked.chum5</file><connection>2</connection><loyalty>2</loyalty><group>False</group><type>Contact</type></contact>
    <contact><guid>readonly</guid><name>Read only</name><connection>2</connection><loyalty>2</loyalty><readonly /><type>Contact</type></contact>
    <contact><guid>enemy</guid><name>Enemy</name><connection>2</connection><loyalty>1</loyalty><type>Enemy</type></contact>
  </contacts>
</character>
""";
        WorkspaceCollectionItemTarget linked = new(WorkspaceCollectionKind.Contact, "linked");
        WorkspaceCollectionItemTarget readOnly = new(WorkspaceCollectionKind.Contact, "readonly");
        WorkspaceCollectionItemTarget enemy = new(WorkspaceCollectionKind.Contact, "enemy");

        string connectionMutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(linked, ContactConnection: 12));
        Assert.AreEqual(
            "12",
            XDocument.Parse(connectionMutated).Root!.Element("contacts")!.Elements("contact")
                .Single(contact => contact.Element("guid")!.Value == "linked")
                .Element("connection")!.Value);
        string roleMutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionTextRequest(linked, WorkspaceCollectionTextField.Role, "Broker"));
        StringAssert.Contains(roleMutated, "<role>Broker</role>");

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionTextRequest(linked, WorkspaceCollectionTextField.Name, "Cannot change")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionToggleRequest(linked, WorkspaceCollectionToggleField.Group, true)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionToggleRequest(linked, WorkspaceCollectionToggleField.Free, false)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(linked, ContactLoyalty: 5)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(readOnly, ContactConnection: 3)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceDeleteCollectionItemRequest(readOnly)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionToggleRequest(enemy, WorkspaceCollectionToggleField.Family, true)));

        const string creationXml = """
<character><created>False</created><contacts><contact><guid>create</guid><name>Create</name><connection>2</connection><loyalty>2</loyalty><type>Contact</type></contact></contacts></character>
""";
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            creationXml,
            new WorkspacePatchCollectionItemRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Contact, "create"),
                ContactConnection: 7)));
    }

    [TestMethod]
    public void ApplyAttributeEdit_burn_decrements_edge_and_can_cross_the_floor()
    {
        const string xml = """
<character>
  <attributes>
    <attribute>
      <name>EDG</name>
      <base>1</base>
      <karma>0</karma>
      <value>1</value>
      <totalvalue>1</totalvalue>
      <metatypemin>1</metatypemin>
      <metatypemax>6</metatypemax>
      <metatypeaugmax>6</metatypeaugmax>
    </attribute>
  </attributes>
</character>
""";

        string burnedXml = WorkspaceXmlMutationCatalog.ApplyAttributeEdit(
            xml,
            new AttributeEditRequest("Edge", "burn", 0));

        StringAssert.Contains(burnedXml, "<name>EDG</name>");
        StringAssert.Contains(burnedXml, "<base>0</base>");
        StringAssert.Contains(burnedXml, "<karma>0</karma>");
        StringAssert.Contains(burnedXml, "<metatypemin>0</metatypemin>");
        StringAssert.Contains(burnedXml, "<totalvalue>0</totalvalue>");
    }

    [TestMethod]
    public void ApplyAttributeEdit_improve_spends_root_karma_and_appends_expense()
    {
        const string xml = """
<character>
  <created>True</created>
  <karma>15</karma>
  <attributes>
    <attribute>
      <name>Body</name>
      <base>1</base>
      <karma>0</karma>
      <value>1</value>
      <totalvalue>1</totalvalue>
      <metatypemin>1</metatypemin>
      <metatypemax>6</metatypemax>
      <metatypeaugmax>9</metatypeaugmax>
    </attribute>
  </attributes>
</character>
""";

        string improvedXml = WorkspaceXmlMutationCatalog.ApplyAttributeEdit(
            xml,
            new AttributeEditRequest("Body", "improve", 2));
        XDocument document = XDocument.Parse(improvedXml);
        XElement root = document.Root!;
        XElement attribute = root.Element("attributes")!.Elements("attribute").Single();
        XElement expense = root.Element("expenses")!.Elements("expense").Single();

        Assert.AreEqual("5", root.Element("karma")!.Value);
        Assert.AreEqual("1", attribute.Element("base")!.Value);
        Assert.AreEqual("1", attribute.Element("karma")!.Value);
        Assert.AreEqual("2", attribute.Element("totalvalue")!.Value);
        Assert.AreEqual("10", expense.Element("amount")!.Value);
        Assert.AreEqual("Improve Body", expense.Element("reason")!.Value);
        Assert.AreEqual("Karma", expense.Element("type")!.Value);
        Assert.AreEqual("False", expense.Element("refund")!.Value);
    }

    [TestMethod]
    public void ApplyAttributeEdit_improve_restores_burned_edge_before_adding_karma()
    {
        const string xml = """
<character>
  <created>True</created>
  <karma>15</karma>
  <attributes>
    <attribute>
      <name>EDG</name>
      <base>0</base>
      <karma>0</karma>
      <value>0</value>
      <totalvalue>0</totalvalue>
      <metatypemin>0</metatypemin>
      <metatypemax>6</metatypemax>
      <metatypeaugmax>6</metatypeaugmax>
    </attribute>
  </attributes>
</character>
""";

        string restoredXml = WorkspaceXmlMutationCatalog.ApplyAttributeEdit(
            xml,
            new AttributeEditRequest("Edge", "improve", 1));
        XDocument document = XDocument.Parse(restoredXml);
        XElement root = document.Root!;
        XElement attribute = root.Element("attributes")!.Elements("attribute").Single();
        XElement expense = root.Element("expenses")!.Elements("expense").Single();

        Assert.AreEqual("10", root.Element("karma")!.Value);
        Assert.AreEqual("1", attribute.Element("base")!.Value);
        Assert.AreEqual("0", attribute.Element("karma")!.Value);
        Assert.AreEqual("1", attribute.Element("metatypemin")!.Value);
        Assert.AreEqual("1", attribute.Element("totalvalue")!.Value);
        Assert.AreEqual("5", expense.Element("amount")!.Value);
        Assert.AreEqual("Improve Edge", expense.Element("reason")!.Value);
    }

    [TestMethod]
    public void ApplyCollectionMutation_edits_and_deletes_only_type_matched_pets()
    {
        const string xml = """
<character>
  <contacts>
    <contact><guid>shared</guid><name>Contact</name><type>Contact</type></contact>
    <contact><guid>shared</guid><name>Rex</name><metatype>Dog</metatype><notes>Old</notes><type>Pet</type></contact>
    <contact><guid>linked</guid><name>Linked</name><metatype>Wolf</metatype><notes>Old link note</notes><file>linked.chum5</file><readonly /><type>Pet</type></contact>
  </contacts>
</character>
""";
        WorkspaceCollectionItemTarget pet = new(WorkspaceCollectionKind.Pet, "shared");
        WorkspaceCollectionItemTarget linked = new(WorkspaceCollectionKind.Pet, "linked");

        string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(
                pet,
                TextValues: new Dictionary<WorkspaceCollectionTextField, string?>
                {
                    [WorkspaceCollectionTextField.Name] = "Cerberus",
                    [WorkspaceCollectionTextField.Metatype] = "Hell Hound",
                    [WorkspaceCollectionTextField.Notes] = "Three bowls."
                }));
        XElement[] records = XDocument.Parse(mutated).Root!.Element("contacts")!.Elements("contact").ToArray();
        XElement contact = records.Single(record => record.Element("type")!.Value == "Contact");
        XElement editedPet = records.Single(record => record.Element("type")!.Value == "Pet" && record.Element("guid")!.Value == "shared");
        Assert.AreEqual("Contact", contact.Element("name")!.Value);
        Assert.AreEqual("Cerberus", editedPet.Element("name")!.Value);
        Assert.AreEqual("Hell Hound", editedPet.Element("metatype")!.Value);
        Assert.AreEqual("Three bowls.", editedPet.Element("notes")!.Value);

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionTextRequest(linked, WorkspaceCollectionTextField.Name, "Cannot change")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionTextRequest(linked, WorkspaceCollectionTextField.Metatype, "Cannot change")));
        string notesMutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionTextRequest(linked, WorkspaceCollectionTextField.Notes, "Still editable"));
        StringAssert.Contains(notesMutated, "<notes>Still editable</notes>");

        string deleted = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceDeleteCollectionItemRequest(linked));
        Assert.IsFalse(XDocument.Parse(deleted).Descendants("guid").Any(node => node.Value == "linked"));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionTextRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Contact, "linked"),
                WorkspaceCollectionTextField.Name,
                "Wrong kind")));
    }

    [TestMethod]
    public void ApplyCollectionMutation_attaches_and_removes_a_linked_runner_without_overwriting_saved_identity()
    {
        const string xml = """
<character>
  <contacts>
    <contact><guid>shared</guid><name>Original contact</name><metatype>Human</metatype><gender>Female</gender><age>38</age><type>Contact</type></contact>
    <contact><guid>shared</guid><name>Original pet</name><metatype>Dog</metatype><type>Pet</type></contact>
  </contacts>
</character>
""";
        WorkspaceCollectionItemTarget contactTarget = new(WorkspaceCollectionKind.Contact, "shared");
        CharacterLinkedDocument identity = new(
            CharacterName: "Neon Fox",
            Name: "Aiko Tanaka",
            Alias: "Neon Fox",
            Metatype: "Elf",
            Metavariant: "Dryad",
            Gender: "Non-binary",
            Age: "29");
        string privateFile = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "chummercomplete-test",
            "linked-characters",
            "contact-shared.chum5lz"));

        string attached = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetLinkedCharacterRequest(
                contactTarget,
                privateFile,
                "linked-characters/contact-shared.chum5lz",
                "Neon Fox.chum5lz",
                identity));

        XElement[] contacts = XDocument.Parse(attached).Root!.Element("contacts")!.Elements("contact").ToArray();
        XElement contact = contacts.Single(item => item.Element("type")!.Value == "Contact");
        XElement pet = contacts.Single(item => item.Element("type")!.Value == "Pet");
        Assert.AreEqual("Original contact", contact.Element("name")!.Value);
        Assert.AreEqual("Human", contact.Element("metatype")!.Value);
        Assert.AreEqual("Original pet", pet.Element("name")!.Value);
        Assert.AreEqual(privateFile, contact.Element("file")!.Value);
        Assert.AreEqual("linked-characters/contact-shared.chum5lz", contact.Element("relative")!.Value);
        XElement snapshot = contact.Element("chummercomplete")!.Element("linkedcharacter")!;
        Assert.AreEqual("Neon Fox.chum5lz", snapshot.Element("displayname")!.Value);
        Assert.AreEqual("Neon Fox", snapshot.Element("name")!.Value);
        Assert.AreEqual("Elf (Dryad)", snapshot.Element("metatype")!.Value);
        Assert.AreEqual("Non-binary", snapshot.Element("gender")!.Value);
        Assert.AreEqual("29", snapshot.Element("age")!.Value);

        string removed = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            attached,
            new WorkspaceRemoveLinkedCharacterRequest(contactTarget));
        XElement restored = XDocument.Parse(removed).Root!.Element("contacts")!.Elements("contact")
            .Single(item => item.Element("type")!.Value == "Contact");
        Assert.AreEqual("Original contact", restored.Element("name")!.Value);
        Assert.AreEqual("Human", restored.Element("metatype")!.Value);
        Assert.AreEqual(string.Empty, restored.Element("file")!.Value);
        Assert.AreEqual(string.Empty, restored.Element("relative")!.Value);
        Assert.IsNull(restored.Element("chummercomplete"));
    }

    [TestMethod]
    public void ApplyCollectionMutation_rejects_unsafe_or_type_confused_linked_runner_targets()
    {
        const string xml = """
<character><contacts>
  <contact><guid>shared</guid><name>Contact</name><type>Contact</type></contact>
  <contact><guid>shared</guid><name>Pet</name><type>Pet</type></contact>
</contacts></character>
""";
        CharacterLinkedDocument identity = new("Runner", "Runner", string.Empty, "Human", string.Empty, string.Empty, string.Empty);
        WorkspaceCollectionItemTarget contactTarget = new(WorkspaceCollectionKind.Contact, "shared");
        string validFile = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "linked-characters", "runner.chum5"));

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetLinkedCharacterRequest(
                contactTarget,
                Path.GetFullPath(Path.Combine(Path.GetTempPath(), "notlinked-characters", "runner.chum5")),
                "linked-characters/runner.chum5",
                "runner.chum5",
                identity)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetLinkedCharacterRequest(
                contactTarget,
                validFile,
                "linked-characters/../runner.chum5",
                "runner.chum5",
                identity)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetLinkedCharacterRequest(
                contactTarget,
                Path.ChangeExtension(validFile, ".txt"),
                "linked-characters/runner.txt",
                "runner.txt",
                identity)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetLinkedCharacterRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Contact, "shared", WorkspaceNestedCollectionKind.Gear, "nested"),
                validFile,
                "linked-characters/runner.chum5",
                "runner.chum5",
                identity)));

        string petAttached = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetLinkedCharacterRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Pet, "shared"),
                validFile,
                "linked-characters/runner.chum5",
                "runner.chum5",
                identity));
        XElement[] records = XDocument.Parse(petAttached).Root!.Element("contacts")!.Elements("contact").ToArray();
        Assert.IsNull(records.Single(item => item.Element("type")!.Value == "Contact").Element("file"));
        Assert.AreEqual(validFile, records.Single(item => item.Element("type")!.Value == "Pet").Element("file")!.Value);
    }

    [TestMethod]
    public void ApplyQuickAdd_creates_a_type_correct_pet()
    {
        string mutated = WorkspaceXmlMutationCatalog.ApplyQuickAdd(
            "<character />",
            new WorkspaceQuickAddRequest(WorkspaceQuickAddKinds.Pet, "Rex"));
        XElement pet = XDocument.Parse(mutated).Root!.Element("contacts")!.Element("contact")!;

        Assert.AreEqual("Rex", pet.Element("name")!.Value);
        Assert.AreEqual("Pet", pet.Element("type")!.Value);
        Assert.IsFalse(string.IsNullOrWhiteSpace(pet.Element("guid")!.Value));
    }

    private sealed class FixedSourceDataResolver : ICharacterSourceDataResolver
    {
        private static readonly ICharacterSourceDataContext Context = new FixedSourceDataContext();

        public ICharacterSourceDataContext? TryCreateContext(string characterXml)
            => Context;
    }

    private sealed class BookSourceDataResolver(params string[] enabledBooks) : ICharacterSourceDataResolver
    {
        private readonly ICharacterSourceDataContext _context = new BookSourceDataContext(enabledBooks);

        public ICharacterSourceDataContext? TryCreateContext(string characterXml)
            => _context;
    }

    private sealed class BookSourceDataContext(params string[] enabledBooks) : ICharacterSourceDataContext
    {
        private readonly HashSet<string> _enabledBooks = enabledBooks.ToHashSet(StringComparer.OrdinalIgnoreCase);

        public bool TryIsBookEnabled(string sourceCode, out bool enabled)
        {
            enabled = _enabledBooks.Contains(sourceCode);
            return !string.IsNullOrWhiteSpace(sourceCode);
        }

        public bool TryResolveCyberwareGradeDeviceRating(
            string gradeName,
            string improvementSource,
            out int deviceRating)
        {
            deviceRating = 0;
            return false;
        }

        public bool TryResolveVehicleModBonuses(
            string sourceId,
            string name,
            out CharacterVehicleModSourceBonuses bonuses)
        {
            bonuses = CharacterVehicleModSourceBonuses.Empty;
            return false;
        }
    }

    private sealed class FixedSourceDataContext : ICharacterSourceDataContext
    {
        public bool TryResolveCyberwareGradeDeviceRating(
            string gradeName,
            string improvementSource,
            out int deviceRating)
        {
            deviceRating = 4;
            return string.Equals(gradeName, "Standard", StringComparison.Ordinal)
                && string.Equals(improvementSource, "Cyberware", StringComparison.Ordinal);
        }

        public bool TryResolveVehicleModBonuses(
            string sourceId,
            string name,
            out CharacterVehicleModSourceBonuses bonuses)
        {
            bonuses = new CharacterVehicleModSourceBonuses(
                BodyExpression: "Rating + 1",
                DeviceRatingExpression: "2",
                MatrixConditionExpression: "3",
                WirelessBodyExpression: string.Empty,
                WirelessDeviceRatingExpression: string.Empty,
                WirelessMatrixConditionExpression: string.Empty);
            return string.Equals(sourceId, ResolverVehicleModId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(name, "Gyro-Stabilization", StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void ApplyCollectionMutation_updates_and_deletes_only_the_selected_spirit_by_stable_id()
    {
        const string xml = """
<character>
  <created>True</created>
  <alias>Preserve me</alias>
  <spirits>
    <spirit><guid>spirit-1</guid><name>Fire Spirit</name><notes>Old note</notes><services>2</services><bound>False</bound></spirit>
    <spirit><guid>spirit-2</guid><name>Water Spirit</name><notes>Unchanged</notes><services>5</services><bound>False</bound></spirit>
  </spirits>
</character>
""";
        WorkspaceCollectionItemTarget target = new(WorkspaceCollectionKind.Spirit, "spirit-1");

        string patched = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(
                target,
                TextValues: new Dictionary<WorkspaceCollectionTextField, string?>
                {
                    [WorkspaceCollectionTextField.Name] = "Ember",
                    [WorkspaceCollectionTextField.Notes] = "On call"
                },
                ToggleValues: new Dictionary<WorkspaceCollectionToggleField, bool>
                {
                    [WorkspaceCollectionToggleField.Bound] = true
                },
                IntegerValues: new Dictionary<WorkspaceCollectionIntegerField, int>
                {
                    [WorkspaceCollectionIntegerField.Services] = 7
                }));

        XElement patchedRoot = XDocument.Parse(patched).Root!;
        XElement selected = patchedRoot.Descendants("spirit")
            .Single(spirit => spirit.Element("guid")?.Value == "spirit-1");
        XElement untouched = patchedRoot.Descendants("spirit")
            .Single(spirit => spirit.Element("guid")?.Value == "spirit-2");
        Assert.AreEqual("Ember", selected.Element("name")?.Value);
        Assert.AreEqual("On call", selected.Element("notes")?.Value);
        Assert.AreEqual("7", selected.Element("services")?.Value);
        Assert.AreEqual("True", selected.Element("bound")?.Value);
        Assert.AreEqual("Water Spirit", untouched.Element("name")?.Value);
        Assert.AreEqual("Unchanged", untouched.Element("notes")?.Value);
        Assert.AreEqual("5", untouched.Element("services")?.Value);
        Assert.AreEqual("False", untouched.Element("bound")?.Value);
        Assert.AreEqual("Preserve me", patchedRoot.Element("alias")?.Value);

        string deleted = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            patched,
            new WorkspaceDeleteCollectionItemRequest(target));
        XElement deletedRoot = XDocument.Parse(deleted).Root!;
        Assert.IsFalse(deletedRoot.Descendants("spirit")
            .Any(spirit => spirit.Element("guid")?.Value == "spirit-1"));
        Assert.AreEqual(
            "Water Spirit",
            deletedRoot.Descendants("spirit").Single().Element("name")?.Value);
    }

    [TestMethod]
    public void Projected_spirit_fields_expose_services_and_gate_bound_until_career_mode()
    {
        JsonObject section = new()
        {
            ["created"] = false,
            ["spirits"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = "spirit-1",
                    ["name"] = "Fire Spirit",
                    ["notes"] = "Keep at arm's length.",
                    ["customName"] = "Torch",
                    ["services"] = 2,
                    ["bound"] = false
                }
            }
        };

        WorkspaceCollectionItemEditorState item = WorkspaceCollectionEditorProjector
            .TryProject("spirits", section)!
            .Items.Single();

        Assert.AreEqual(WorkspaceCollectionKind.Spirit, item.Target.Kind);
        Assert.AreEqual("spirit-1", item.Target.ItemId);
        CollectionAssert.AreEqual(
            new[]
            {
                WorkspaceCollectionTextField.Name,
                WorkspaceCollectionTextField.Notes,
                WorkspaceCollectionTextField.CustomName
            },
            item.TextValues.Select(value => value.Field).ToArray());
        Assert.AreEqual(
            "Fire Spirit",
            item.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Name).Value);
        Assert.AreEqual(
            "Keep at arm's length.",
            item.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Notes).Value);
        Assert.IsFalse(
            item.ToggleValues.Single(value => value.Field == WorkspaceCollectionToggleField.Bound).Value);
        Assert.IsFalse(
            item.ToggleValues.Single(value => value.Field == WorkspaceCollectionToggleField.Bound).IsEnabled);
        WorkspaceCollectionIntegerValueState services = item.IntegerValues.Single();
        Assert.AreEqual(WorkspaceCollectionIntegerField.Services, services.Field);
        Assert.AreEqual(2, services.Value);
        Assert.AreEqual(0, services.Minimum);
        Assert.AreEqual(int.MaxValue, services.Maximum);
        Assert.IsTrue(item.CanDelete);
        Assert.IsNull(item.Rating);
        Assert.IsNull(item.Quantity);
    }

    [TestMethod]
    public void ApplyCollectionMutation_rejects_precreation_bound_and_negative_services()
    {
        const string xml = """
<character>
  <created>False</created>
  <spirits><spirit><guid>spirit-1</guid><name>Fire Spirit</name><services>2</services><bound>False</bound></spirit></spirits>
</character>
""";
        WorkspaceCollectionItemTarget target = new(WorkspaceCollectionKind.Spirit, "spirit-1");

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionToggleRequest(
                target,
                WorkspaceCollectionToggleField.Bound,
                true)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionIntegerRequest(
                target,
                WorkspaceCollectionIntegerField.Services,
                -1)));
    }

    [TestMethod]
    public void Projected_sprite_force_uses_exact_rating_ceiling_and_persists_only_in_career_mode()
    {
        JsonObject section = new()
        {
            ["created"] = true,
            ["spirits"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = "sprite-1",
                    ["name"] = "Machine Sprite",
                    ["entityType"] = "Sprite",
                    ["force"] = 8,
                    ["services"] = 2,
                    ["bound"] = true,
                    ["forceMaximum"] = 8,
                    ["forceMaximumExact"] = true,
                    ["forceEditable"] = true
                }
            }
        };

        WorkspaceCollectionItemEditorState item = WorkspaceCollectionEditorProjector
            .TryProject("spirits", section)!
            .Items.Single();
        WorkspaceCollectionIntegerValueState rating = item.IntegerValues.Single(
            value => value.Field == WorkspaceCollectionIntegerField.Force);
        Assert.AreEqual(8, rating.Value);
        Assert.AreEqual(0, rating.Minimum);
        Assert.AreEqual(8, rating.Maximum);
        Assert.IsTrue(rating.IsEnabled);
        Assert.AreEqual("Rating", rating.Label);
        Assert.AreEqual(
            "Registered",
            item.ToggleValues.Single(value => value.Field == WorkspaceCollectionToggleField.Bound).Label);

        const string xml = """
<character>
  <created>True</created>
  <resenabled>True</resenabled>
  <attributes><attribute><name>RES</name><totalvalue>4</totalvalue></attribute></attributes>
  <spirits><spirit><guid>sprite-1</guid><name>Machine Sprite</name><type>Sprite</type><force>8</force></spirit></spirits>
</character>
""";
        WorkspaceCollectionItemTarget target = new(WorkspaceCollectionKind.Spirit, "sprite-1");
        string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionIntegerRequest(
                target,
                WorkspaceCollectionIntegerField.Force,
                7));
        Assert.AreEqual("7", XDocument.Parse(mutated).Descendants("force").Single().Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionIntegerRequest(
                target,
                WorkspaceCollectionIntegerField.Force,
                9)));

        string creation = xml.Replace("<created>True</created>", "<created>False</created>", StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            creation,
            new WorkspaceSetCollectionIntegerRequest(
                target,
                WorkspaceCollectionIntegerField.Force,
                4)));
    }

    [TestMethod]
    public void Spirit_critter_name_is_available_only_when_saved_data_proves_no_linked_runner_path()
    {
        JsonObject section = new()
        {
            ["created"] = true,
            ["spirits"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = "spirit-free",
                    ["name"] = "Fire Spirit",
                    ["critterName"] = "Ember",
                    ["critterNameEditableExact"] = true
                },
                new JsonObject
                {
                    ["guid"] = "spirit-linked",
                    ["name"] = "Water Spirit",
                    ["critterName"] = "Tide",
                    ["critterNameEditableExact"] = false
                }
            }
        };

        WorkspaceCollectionEditorState editor = WorkspaceCollectionEditorProjector.TryProject("spirits", section)!;
        WorkspaceCollectionItemEditorState free = editor.Items.Single(item => item.Target.ItemId == "spirit-free");
        Assert.AreEqual(
            "Ember",
            free.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.CritterName).Value);
        Assert.IsTrue(free.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.CritterName).IsEnabled);
        Assert.IsFalse(editor.Items.Single(item => item.Target.ItemId == "spirit-linked")
            .TextValues.Any(value => value.Field == WorkspaceCollectionTextField.CritterName));

        const string xml = """
<character><spirits>
  <spirit><guid>spirit-free</guid><name>Fire Spirit</name><crittername>Ember</crittername></spirit>
  <spirit><guid>spirit-linked</guid><name>Water Spirit</name><crittername>Tide</crittername><file>linked.chum5</file></spirit>
</spirits></character>
""";
        string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionTextRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Spirit, "spirit-free"),
                WorkspaceCollectionTextField.CritterName,
                "Cinder"));
        Assert.AreEqual("Cinder", XDocument.Parse(mutated).Descendants("crittername").First().Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionTextRequest(
                new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Spirit, "spirit-linked"),
                WorkspaceCollectionTextField.CritterName,
                "Flood")));
    }
}
