#nullable enable annotations

using System;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class WorkspaceXmlMutationCatalogTests
{
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
        }
    }
}
