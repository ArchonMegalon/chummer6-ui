using System.Text.Json.Nodes;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class WorkspaceCollectionEditorProjectorTests
{
    [TestMethod]
    public void TryProject_projects_all_closed_top_level_collection_kinds_by_stable_id()
    {
        (string SectionId, string CollectionProperty, WorkspaceCollectionKind Kind)[] cases =
        [
            ("gear", "gear", WorkspaceCollectionKind.Gear),
            ("weapons", "weapons", WorkspaceCollectionKind.Weapon),
            ("armors", "armors", WorkspaceCollectionKind.Armor),
            ("skills", "skills", WorkspaceCollectionKind.Skill),
            ("contacts", "contacts", WorkspaceCollectionKind.Contact),
            ("vehicles", "vehicles", WorkspaceCollectionKind.Vehicle),
            ("qualities", "qualities", WorkspaceCollectionKind.Quality),
            ("drugs", "drugs", WorkspaceCollectionKind.Drug),
            ("cyberwares", "cyberwares", WorkspaceCollectionKind.Cyberware),
            ("spells", "spells", WorkspaceCollectionKind.Spell),
            ("powers", "powers", WorkspaceCollectionKind.Power),
            ("complexforms", "complexForms", WorkspaceCollectionKind.ComplexForm),
            ("aiprograms", "aiPrograms", WorkspaceCollectionKind.MatrixProgram),
            ("initiationgrades", "initiationGrades", WorkspaceCollectionKind.InitiationGrade),
            ("spirits", "spirits", WorkspaceCollectionKind.Spirit),
            ("critterpowers", "critterPowers", WorkspaceCollectionKind.CritterPower)
        ];

        foreach ((string sectionId, string collectionProperty, WorkspaceCollectionKind kind) in cases)
        {
            JsonObject item = new()
            {
                ["guid"] = $"id-{sectionId}",
                ["name"] = $"Name {sectionId}",
                ["reward"] = "Masking",
                ["grade"] = 1
            };
            JsonObject section = new() { [collectionProperty] = new JsonArray(item) };

            WorkspaceCollectionEditorState? result = WorkspaceCollectionEditorProjector.TryProject(sectionId, section);

            Assert.IsNotNull(result, $"Section '{sectionId}' should have a typed editor projection.");
            Assert.AreEqual(kind, result.Kind);
            Assert.IsNull(result.NestedKind);
            Assert.HasCount(1, result.Items);
            Assert.AreEqual($"id-{sectionId}", result.Items[0].Target.ItemId);
            Assert.AreEqual(kind, result.Items[0].Target.Kind);
            Assert.IsTrue(result.Items[0].CanDelete);
            Assert.IsTrue(result.Items[0].CanMove);
        }
    }

    [TestMethod]
    public void TryProject_emits_typed_values_and_capabilities_for_atomic_form_save()
    {
        JsonObject section = new()
        {
            ["gear"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = "gear-1",
                    ["name"] = "Renraku Sensei",
                    ["category"] = "Commlinks",
                    ["source"] = "Core",
                    ["notes"] = "Primary",
                    ["customName"] = "Ghostline",
                    ["location"] = "Jacket",
                    ["rating"] = "3",
                    ["quantity"] = 2.5m,
                    ["equipped"] = true,
                    ["wirelessEnabled"] = true,
                    ["homeNode"] = false
                }
            }
        };

        WorkspaceCollectionItemEditorState item = WorkspaceCollectionEditorProjector
            .TryProject("gear", section)!.Items.Single();

        Assert.AreEqual("Renraku Sensei", item.Label);
        Assert.AreEqual("Ghostline", item.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.CustomName).Value);
        Assert.AreEqual(3, item.Rating?.Value);
        Assert.AreEqual(2.5m, item.Quantity?.Value);
        Assert.IsTrue(item.ToggleValues.Single(value => value.Field == WorkspaceCollectionToggleField.Equipped).Value);
        Assert.IsTrue(item.ToggleValues.Single(value => value.Field == WorkspaceCollectionToggleField.WirelessEnabled).Value);
        Assert.IsFalse(item.ToggleValues.Single(value => value.Field == WorkspaceCollectionToggleField.HomeNode).Value);
        CollectionAssert.AreEqual(
            new[] { WorkspaceNestedCollectionKind.Gear },
            item.AddableNestedKinds.ToArray());
    }

    [TestMethod]
    public void TryProject_emits_exact_safe_vehicle_armor_and_weapon_condition_states()
    {
        JsonObject section = new()
        {
            ["vehicles"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = "vehicle-1",
                    ["name"] = "Roadmaster",
                    ["physicalDamage"] = 4,
                    ["physicalConditionMaximum"] = 15,
                    ["physicalConditionMaximumExact"] = true,
                    ["matrixDamage"] = 2,
                    ["matrixConditionMaximum"] = 10,
                    ["matrixConditionMaximumExact"] = true,
                    ["careerEditable"] = true
                },
                new JsonObject
                {
                    ["guid"] = "vehicle-2",
                    ["name"] = "Unresolved drone",
                    ["physicalDamage"] = 2,
                    ["physicalConditionMaximum"] = 0,
                    ["physicalConditionMaximumExact"] = false,
                    ["careerEditable"] = true
                }
            }
        };

        WorkspaceCollectionEditorState result = WorkspaceCollectionEditorProjector.TryProject("vehicles", section)!;

        WorkspaceItemConditionMonitorState exact = result.Items[0].PhysicalConditionMonitor!;
        Assert.AreEqual("Physical damage", exact.Label);
        Assert.AreEqual(4, exact.Filled);
        Assert.AreEqual(15, exact.Maximum);
        Assert.IsTrue(exact.MaximumExact);
        Assert.IsTrue(exact.Editable);
        WorkspaceItemConditionMonitorState matrix = result.Items[0].MatrixConditionMonitor!;
        Assert.AreEqual("Matrix damage", matrix.Label);
        Assert.AreEqual(2, matrix.Filled);
        Assert.AreEqual(10, matrix.Maximum);
        Assert.IsTrue(matrix.MaximumExact);
        Assert.IsTrue(matrix.Editable);
        WorkspaceItemConditionMonitorState unresolved = result.Items[1].PhysicalConditionMonitor!;
        Assert.AreEqual(2, unresolved.Filled);
        Assert.AreEqual(0, unresolved.Maximum);
        Assert.IsFalse(unresolved.MaximumExact);
        Assert.IsFalse(unresolved.Editable);
        Assert.IsFalse(result.Items[1].MatrixConditionMonitor!.MaximumExact);

        JsonObject armorSection = new()
        {
            ["armors"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = "armor-1",
                    ["name"] = "Armor jacket",
                    ["matrixDamage"] = 3,
                    ["matrixConditionMaximum"] = 13,
                    ["matrixConditionMaximumExact"] = true,
                    ["careerEditable"] = true
                }
            }
        };

        WorkspaceCollectionEditorState armor = WorkspaceCollectionEditorProjector.TryProject("armors", armorSection)!;

        Assert.IsNull(armor.Items[0].PhysicalConditionMonitor);
        Assert.AreEqual(3, armor.Items[0].MatrixConditionMonitor?.Filled);
        Assert.AreEqual(13, armor.Items[0].MatrixConditionMonitor?.Maximum);
        Assert.IsTrue(armor.Items[0].MatrixConditionMonitor?.Editable);

        JsonObject weaponSection = new()
        {
            ["weapons"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = "weapon-1",
                    ["name"] = "Smartgun",
                    ["matrixDamage"] = 2,
                    ["matrixConditionMaximum"] = 10,
                    ["matrixConditionMaximumExact"] = true,
                    ["careerEditable"] = true
                }
            }
        };

        WorkspaceCollectionEditorState weapon = WorkspaceCollectionEditorProjector.TryProject("weapons", weaponSection)!;

        Assert.IsNull(weapon.Items[0].PhysicalConditionMonitor);
        Assert.AreEqual(2, weapon.Items[0].MatrixConditionMonitor?.Filled);
        Assert.AreEqual(10, weapon.Items[0].MatrixConditionMonitor?.Maximum);
        Assert.IsTrue(weapon.Items[0].MatrixConditionMonitor?.Editable);
    }

    [TestMethod]
    public void TryProject_projects_nested_targets_with_parent_and_child_ids()
    {
        (string SectionId, string CollectionProperty, string ParentProperty, string ChildProperty,
            WorkspaceCollectionKind Kind, WorkspaceNestedCollectionKind NestedKind)[] cases =
        [
            ("weaponaccessories", "accessories", "weaponGuid", "accessoryGuid", WorkspaceCollectionKind.Weapon, WorkspaceNestedCollectionKind.WeaponAccessory),
            ("armormods", "armorMods", "armorGuid", "modGuid", WorkspaceCollectionKind.Armor, WorkspaceNestedCollectionKind.ArmorMod),
            ("vehiclemods", "vehicleMods", "vehicleGuid", "modGuid", WorkspaceCollectionKind.Vehicle, WorkspaceNestedCollectionKind.VehicleMod)
        ];

        foreach (var testCase in cases)
        {
            JsonObject section = new()
            {
                [testCase.CollectionProperty] = new JsonArray
                {
                    new JsonObject
                    {
                        [testCase.ParentProperty] = "parent-1",
                        [testCase.ChildProperty] = "child-1",
                        ["name"] = "Nested item",
                        ["rating"] = 2,
                        ["equipped"] = true
                    }
                }
            };

            WorkspaceCollectionEditorState? result = WorkspaceCollectionEditorProjector.TryProject(testCase.SectionId, section);

            Assert.IsNotNull(result);
            Assert.AreEqual(testCase.NestedKind, result.NestedKind);
            WorkspaceCollectionItemTarget target = result.Items.Single().Target;
            Assert.AreEqual(testCase.Kind, target.Kind);
            Assert.AreEqual("parent-1", target.ItemId);
            Assert.AreEqual(testCase.NestedKind, target.NestedKind);
            Assert.AreEqual("child-1", target.NestedItemId);
        }
    }

    [TestMethod]
    public void TryProject_treats_flattened_cyberware_children_as_nested_stable_targets()
    {
        JsonObject section = new()
        {
            ["cyberwares"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = "cyber-parent",
                    ["name"] = "Modular arm",
                    ["parentGuid"] = ""
                },
                new JsonObject
                {
                    ["guid"] = "cyber-child",
                    ["name"] = "Smuggling compartment",
                    ["parentGuid"] = "cyber-parent",
                    ["rating"] = "2",
                    ["matrixDamage"] = 1,
                    ["matrixConditionMaximum"] = 10,
                    ["matrixConditionMaximumExact"] = true,
                    ["careerEditable"] = true
                }
            }
        };

        WorkspaceCollectionEditorState? result = WorkspaceCollectionEditorProjector.TryProject("cyberwares", section);

        Assert.IsNotNull(result);
        WorkspaceCollectionItemEditorState child = result.Items.Single(item => item.Target.NestedItemId == "cyber-child");
        Assert.AreEqual("cyber-parent", child.Target.ItemId);
        Assert.AreEqual(WorkspaceNestedCollectionKind.CyberwarePlugin, child.Target.NestedKind);
        Assert.IsNotNull(child.Rating);
        Assert.AreEqual(10, child.MatrixConditionMonitor?.Maximum);
        Assert.IsTrue(child.MatrixConditionMonitor?.Editable);
        Assert.IsEmpty(child.AddableNestedKinds);
    }

    [TestMethod]
    public void TryProject_treats_flattened_gear_children_as_matrix_editable_nested_targets()
    {
        JsonObject section = new()
        {
            ["gear"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = "gear-parent",
                    ["name"] = "Cyberdeck",
                    ["parentGuid"] = "",
                    ["matrixDamage"] = 2,
                    ["matrixConditionMaximum"] = 12,
                    ["matrixConditionMaximumExact"] = true,
                    ["careerEditable"] = true
                },
                new JsonObject
                {
                    ["guid"] = "gear-child",
                    ["name"] = "Module",
                    ["parentGuid"] = "gear-parent",
                    ["matrixDamage"] = 1,
                    ["matrixConditionMaximum"] = 10,
                    ["matrixConditionMaximumExact"] = true,
                    ["careerEditable"] = true
                }
            }
        };

        WorkspaceCollectionEditorState result = WorkspaceCollectionEditorProjector.TryProject("gear", section)!;

        WorkspaceCollectionItemEditorState child = result.Items.Single(item => item.Target.NestedItemId == "gear-child");
        Assert.AreEqual("gear-parent", child.Target.ItemId);
        Assert.AreEqual(WorkspaceNestedCollectionKind.Gear, child.Target.NestedKind);
        Assert.AreEqual(10, child.MatrixConditionMonitor?.Maximum);
        Assert.IsTrue(child.MatrixConditionMonitor?.Editable);
        Assert.IsEmpty(child.AddableNestedKinds);
    }

    [TestMethod]
    public void TryProject_projects_complete_contact_fields_ratings_and_editability()
    {
        JsonObject contactNode = new()
        {
            ["guid"] = "contact-1",
            ["name"] = "Ms. Johnson",
            ["notes"] = "Keep it discreet.",
            ["customName"] = "J",
            ["role"] = "Fixer",
            ["location"] = "Vienna",
            ["metatype"] = "Elf",
            ["gender"] = "Female",
            ["age"] = "42",
            ["contactType"] = "Professional",
            ["preferredPayment"] = "Credstick",
            ["hobbiesVice"] = "Urban exploration",
            ["personalLife"] = "Private",
            ["groupName"] = "Night Market",
            ["connection"] = 6,
            ["connectionMaximum"] = 12,
            ["loyalty"] = 4,
            ["isGroup"] = false,
            ["free"] = true,
            ["family"] = true,
            ["blackmail"] = false,
            ["identityEditable"] = true,
            ["connectionEditable"] = true,
            ["loyaltyEditable"] = false,
            ["groupEditable"] = true,
            ["freeEditable"] = false,
            ["familyEditable"] = true,
            ["blackmailEditable"] = true,
            ["canDelete"] = true,
            ["editSemanticsExact"] = true
        };
        JsonObject section = new()
        {
            ["contacts"] = new JsonArray(contactNode)
        };

        WorkspaceCollectionItemEditorState contact = WorkspaceCollectionEditorProjector
            .TryProject("contacts", section)!
            .Items.Single();

        Assert.HasCount(13, contact.TextValues);
        Assert.AreEqual(
            "Professional",
            contact.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.ContactType).Value);
        Assert.AreEqual(
            "Night Market",
            contact.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.GroupName).Value);
        Assert.IsTrue(contact.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Name).IsEnabled);
        Assert.AreEqual(6, contact.Contact?.Connection);
        Assert.AreEqual(12, contact.Contact?.ConnectionMaximum);
        Assert.IsTrue(contact.Contact?.ConnectionEditable);
        Assert.AreEqual(4, contact.Contact?.Loyalty);
        Assert.IsFalse(contact.Contact?.LoyaltyEditable);
        Assert.IsTrue(contact.Contact?.Exact);
        Assert.IsTrue(contact.ToggleValues.Single(value => value.Field == WorkspaceCollectionToggleField.Free).Value);
        Assert.IsFalse(contact.ToggleValues.Single(value => value.Field == WorkspaceCollectionToggleField.Free).IsEnabled);
        Assert.IsTrue(contact.ToggleValues.Single(value => value.Field == WorkspaceCollectionToggleField.Family).IsEnabled);
        Assert.IsTrue(contact.CanDelete);

        contactNode["identityEditable"] = false;
        contactNode["canDelete"] = false;
        WorkspaceCollectionItemEditorState linked = WorkspaceCollectionEditorProjector
            .TryProject("contacts", section)!
            .Items.Single();

        Assert.IsFalse(linked.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Name).IsEnabled);
        Assert.IsFalse(linked.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Metatype).IsEnabled);
        Assert.IsTrue(linked.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Role).IsEnabled);
        Assert.IsFalse(linked.CanDelete);
    }

    [TestMethod]
    public void TryProject_projects_exact_pet_fields_and_link_gates()
    {
        JsonObject petNode = new()
        {
            ["guid"] = "pet-1",
            ["name"] = "Rex",
            ["metatype"] = "Hell Hound",
            ["notes"] = "Likes synth-meat.",
            ["identityEditable"] = false,
            ["canDelete"] = true,
            ["editSemanticsExact"] = true
        };
        JsonObject section = new()
        {
            ["contacts"] = new JsonArray(petNode)
        };

        WorkspaceCollectionEditorState editor = WorkspaceCollectionEditorProjector.TryProject("pets", section)!;
        WorkspaceCollectionItemEditorState pet = editor.Items.Single();

        Assert.AreEqual(WorkspaceCollectionKind.Pet, editor.Kind);
        Assert.AreEqual(WorkspaceCollectionKind.Pet, pet.Target.Kind);
        CollectionAssert.AreEqual(
            new[]
            {
                WorkspaceCollectionTextField.Name,
                WorkspaceCollectionTextField.Metatype,
                WorkspaceCollectionTextField.Notes
            },
            pet.TextValues.Select(value => value.Field).ToArray());
        Assert.IsFalse(pet.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Name).IsEnabled);
        Assert.IsFalse(pet.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Metatype).IsEnabled);
        Assert.IsTrue(pet.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Notes).IsEnabled);
        Assert.IsTrue(pet.CanDelete);
        Assert.IsNull(pet.Contact);
        Assert.IsNull(pet.Rating);
        Assert.IsEmpty(pet.ToggleValues);
    }

    [TestMethod]
    public void TryProject_projects_linked_runner_state_and_preserves_identity_edit_gates()
    {
        JsonObject section = new()
        {
            ["contacts"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = "contact-1",
                    ["name"] = "Neon Fox",
                    ["metatype"] = "Elf (Dryad)",
                    ["gender"] = "Non-binary",
                    ["age"] = "29",
                    ["identityEditable"] = false,
                    ["editSemanticsExact"] = true,
                    ["linkedCharacter"] = new JsonObject
                    {
                        ["isLinked"] = true,
                        ["identityResolved"] = true,
                        ["fileName"] = "/private/linked-characters/contact-1.chum5lz",
                        ["relativeFileName"] = "linked-characters/contact-1.chum5lz",
                        ["displayName"] = "Neon Fox.chum5lz"
                    }
                }
            }
        };

        WorkspaceCollectionItemEditorState contact = WorkspaceCollectionEditorProjector
            .TryProject("contacts", section)!
            .Items.Single();

        WorkspaceLinkedCharacterState linked = contact.LinkedCharacter!;
        Assert.IsTrue(linked.IsLinked);
        Assert.IsTrue(linked.IdentityResolved);
        Assert.IsTrue(linked.CanAttach);
        Assert.IsTrue(linked.CanRemove);
        Assert.AreEqual("Neon Fox.chum5lz", linked.DisplayName);
        Assert.AreEqual("linked-characters/contact-1.chum5lz", linked.RelativeFileName);
        Assert.IsFalse(contact.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Name).IsEnabled);
        Assert.IsFalse(contact.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Metatype).IsEnabled);
        Assert.IsFalse(contact.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Gender).IsEnabled);
        Assert.IsFalse(contact.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Age).IsEnabled);
        Assert.IsTrue(contact.TextValues.Single(value => value.Field == WorkspaceCollectionTextField.Role).IsEnabled);
    }

    [TestMethod]
    public void TryProject_fails_closed_for_missing_or_duplicate_stable_identity()
    {
        JsonObject missing = new()
        {
            ["contacts"] = new JsonArray(new JsonObject { ["name"] = "No ID" })
        };
        JsonObject duplicate = new()
        {
            ["contacts"] = new JsonArray(
                new JsonObject { ["guid"] = "same", ["name"] = "One" },
                new JsonObject { ["guid"] = "SAME", ["name"] = "Two" })
        };
        JsonObject orphanedGear = new()
        {
            ["gear"] = new JsonArray(
                new JsonObject { ["guid"] = "child", ["name"] = "Child", ["depth"] = 1, ["parentGuid"] = "" })
        };

        Assert.IsNull(WorkspaceCollectionEditorProjector.TryProject("contacts", missing));
        Assert.IsNull(WorkspaceCollectionEditorProjector.TryProject("contacts", duplicate));
        Assert.IsNull(WorkspaceCollectionEditorProjector.TryProject("unknown", duplicate));
        Assert.IsNull(WorkspaceCollectionEditorProjector.TryProject("gear", orphanedGear));
    }
}
