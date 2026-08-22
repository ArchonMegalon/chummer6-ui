using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class GroupMembershipParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("group-membership-tests");

    [TestMethod]
    public void Creation_toggle_changes_only_saved_membership()
    {
        const string xml = "<character><created>False</created><magenabled>True</magenabled><karma>0</karma><notes>keep</notes></character>";
        GroupMembershipEditorState editor = GroupMembershipEditorProjector.Project(xml, WorkspaceId, 4, null);

        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyGroupMembershipEdit(
            xml,
            new GroupMembershipEditRequest(WorkspaceId, 4, editor.Membership, true, false))).Root!;

        Assert.AreEqual("True", root.Element("groupmember")!.Value);
        Assert.AreEqual("0", root.Element("karma")!.Value);
        Assert.AreEqual("keep", root.Element("notes")!.Value);
        Assert.IsNull(root.Element("expenses"));
    }

    [TestMethod]
    public void Career_magician_join_spends_exact_profile_cost_and_records_legacy_undo()
    {
        const string xml = "<character><settings>exact</settings><created>True</created><magenabled>True</magenabled><resenabled>False</resenabled><groupmember>False</groupmember><karma>8</karma></character>";
        var resolver = new ExactResolver(5, 1);
        GroupMembershipEditorState editor = GroupMembershipEditorProjector.Project(xml, WorkspaceId, 7, resolver);

        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyGroupMembershipEdit(
            xml,
            new GroupMembershipEditRequest(WorkspaceId, 7, editor.Membership, true, true),
            resolver)).Root!;

        Assert.AreEqual("True", root.Element("groupmember")!.Value);
        Assert.AreEqual("3", root.Element("karma")!.Value);
        XElement expense = root.Element("expenses")!.Element("expense")!;
        Assert.AreEqual("-5", expense.Element("amount")!.Value);
        Assert.AreEqual("JoinGroup", expense.Element("undo")!.Element("karmatype")!.Value);
        Assert.AreEqual("AddCyberware", expense.Element("undo")!.Element("nuyentype")!.Value);
    }

    [TestMethod]
    public void Career_magician_requires_profile_cost_confirmation_and_fresh_state()
    {
        const string xml = "<character><settings>exact</settings><created>True</created><magenabled>True</magenabled><groupmember>False</groupmember><karma>4</karma></character>";
        var resolver = new ExactResolver(5, 1);
        GroupMembershipEditorState blocked = GroupMembershipEditorProjector.Project(xml, WorkspaceId, 8, resolver);
        Assert.IsFalse(blocked.Membership.CanChange);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyGroupMembershipEdit(
            xml,
            new GroupMembershipEditRequest(WorkspaceId, 8, blocked.Membership, true, true),
            resolver));
    }

    private sealed class ExactResolver(int joinCost, int leaveCost) : ICharacterSourceDataResolver
    {
        public ICharacterSourceDataContext TryCreateContext(string characterXml)
            => new ExactContext(joinCost, leaveCost);
    }

    private sealed class ExactContext(int joinCost, int leaveCost) : ICharacterSourceDataContext
    {
        public bool TryResolveGroupMembershipKarmaCosts(out int join, out int leave)
        {
            join = joinCost;
            leave = leaveCost;
            return true;
        }

        public bool TryResolveCyberwareGradeDeviceRating(string gradeName, string improvementSource, out int deviceRating)
        {
            deviceRating = 0;
            return false;
        }

        public bool TryResolveVehicleModBonuses(string sourceId, string name, out CharacterVehicleModSourceBonuses bonuses)
        {
            bonuses = CharacterVehicleModSourceBonuses.Empty;
            return false;
        }
    }
}
