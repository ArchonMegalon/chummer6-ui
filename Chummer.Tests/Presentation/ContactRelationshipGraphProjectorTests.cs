#nullable enable annotations

using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class ContactRelationshipGraphProjectorTests
{
    [TestMethod]
    public void FromContacts_returns_graph_with_faction_heat_obligation_and_favor_rails()
    {
        CharacterContactsSection contacts = new(
            Count: 3,
            Contacts:
            [
                new CharacterContactSummary("Paz Ortega", "Street doc", "Redmond", 4, 5),
                new CharacterContactSummary("Mina Voss", "Fixer", "Tacoma", 6, 3),
                new CharacterContactSummary("Hexswitch", "Matrix broker", "Bellevue", 3, 2)
            ]);

        ContactRelationshipGraphState? graph = ContactRelationshipGraphProjector.FromContacts(contacts);

        Assert.IsNotNull(graph);
        Assert.AreEqual(3, graph.Nodes.Count);
        Assert.IsTrue(graph.Factions.Count >= 2);
        Assert.IsTrue(graph.HeatRails.Count >= 1);
        Assert.IsTrue(graph.Obligations.Count >= 1);
        Assert.IsTrue(graph.UnresolvedFavors.Count >= 1);
        Assert.IsTrue(graph.Nodes.All(node => node.LinkedContactNames.Count <= 2));
    }

    [TestMethod]
    public void FromContacts_returns_null_when_contacts_are_missing()
    {
        ContactRelationshipGraphState? graph = ContactRelationshipGraphProjector.FromContacts(null);
        Assert.IsNull(graph);
    }
}
