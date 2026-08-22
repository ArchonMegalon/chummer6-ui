using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class TraditionDrainParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("tradition-drain-tests");
    private static readonly Guid TraditionId = Guid.Parse("d87f03c0-8820-4f5f-8362-c05bcbacb64d");
    private static readonly ICharacterSourceDataResolver Resolver = new FixedResolver();

    [TestMethod]
    public void ProjectAndApply_UseExactCatalogAndPreserveUnrelatedNodes()
    {
        string xml = CustomXml("{WIL} + {CHA}");
        TraditionDrainEditorState editor = TraditionDrainEditorProjector.Project(
            xml, WorkspaceId, 4, Resolver);
        Assert.IsTrue(editor.AllowedExpressions.Contains("{WIL} + {LOG}"));

        XElement tradition = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyTraditionDrainEdit(
            xml,
            new TraditionDrainEditRequest(
                WorkspaceId,
                4,
                editor.TraditionId,
                editor.DrainExpression,
                "{WIL} + {LOG}"),
            Resolver)).Root!.Element("tradition")!;

        Assert.AreEqual("{WIL} + {LOG}", tradition.Element("drain")!.Value);
        Assert.AreEqual("Untouched", tradition.Element("extra")!.Value);
    }

    [TestMethod]
    public void Project_RejectsAdeptOnlyPublishedSetAndMissingCatalog()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => TraditionDrainEditorProjector.Project(
            CustomXml(string.Empty, adept: true, magician: false), WorkspaceId, 4, Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() => TraditionDrainEditorProjector.Project(
            PublishedXml("{WIL} + {LOG}"), WorkspaceId, 4, Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() => TraditionDrainEditorProjector.Project(
            CustomXml(string.Empty), WorkspaceId, 4, new MissingResolver()));
    }

    [TestMethod]
    public void Apply_RejectsRevisionBasisDriftAndUnknownExpression()
    {
        string xml = CustomXml("{WIL} + {CHA}");
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyTraditionDrainEdit(
            xml,
            new TraditionDrainEditRequest(
                WorkspaceId, 4, TraditionId, "{WIL} + {INT}", "{WIL} + {LOG}"),
            Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyTraditionDrainEdit(
            xml,
            new TraditionDrainEditRequest(
                WorkspaceId, 4, TraditionId, "{WIL} + {CHA}", "{WIL} + {BOD}"),
            Resolver));
    }

    private static string CustomXml(string drain, bool adept = false, bool magician = true)
        => $"<character><adept>{adept}</adept><magician>{magician}</magician><tradition><sourceid>616ba093-306c-45fc-8f41-0b98c8cccb46</sourceid><guid>{TraditionId:D}</guid><traditiontype>MAG</traditiontype><drain>{drain}</drain><extra>Untouched</extra></tradition></character>";

    private static string PublishedXml(string drain)
        => $"<character><adept>False</adept><magician>True</magician><tradition><sourceid>19320625-bc1a-492f-8904-da6a847e5700</sourceid><guid>{TraditionId:D}</guid><traditiontype>MAG</traditiontype><drain>{drain}</drain></tradition></character>";

    private sealed class FixedResolver : ICharacterSourceDataResolver
    {
        public ICharacterSourceDataContext? TryCreateContext(string characterXml) => new FixedContext();
    }

    private sealed class FixedContext : ICharacterSourceDataContext
    {
        public bool TryResolveTraditionDrainExpressions(out IReadOnlyList<string> expressions)
        {
            expressions = ["{WIL} + {CHA}", "{WIL} + {INT}", "{WIL} + {LOG}"];
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

    private sealed class MissingResolver : ICharacterSourceDataResolver
    {
        public ICharacterSourceDataContext? TryCreateContext(string characterXml) => null;
    }
}
