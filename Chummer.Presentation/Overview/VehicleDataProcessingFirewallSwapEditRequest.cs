using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record VehicleDataProcessingFirewallSwapEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    CharacterVehicleMatrixSwapState Vehicle);

public sealed record VehicleDataProcessingFirewallSwapEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterVehicleMatrixSwapIdentity Identity,
    string ExpectedNodeRevision,
    CharacterVehicleMatrixStat ChangedAttribute,
    CharacterVehicleMatrixStat TargetAttribute);

internal static class VehicleDataProcessingFirewallSwapEditorProjector
{
    public static VehicleDataProcessingFirewallSwapEditorState Project(
        string xml, CharacterWorkspaceId workspaceId, long contentRevision, Guid vehicleId)
    {
        if (contentRevision <= 0)
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before swapping Vehicle Matrix values.");
        return new(workspaceId, contentRevision, ProjectValue(xml, vehicleId));
    }

    internal static CharacterVehicleMatrixSwapState ProjectValue(string xml, Guid vehicleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (vehicleId == Guid.Empty)
            throw new InvalidOperationException("Vehicle Matrix swapping requires a stable Vehicle Guid.");
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" } ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        bool created = ReadBoolean(root, "created");
        XElement vehicle = FindVehicle(root, vehicleId);
        if (!CharacterVehicleMatrixSwapRules.TryCreateState(
                new CharacterVehicleMatrixSwapIdentity(vehicleId), created,
                ReadDisplayName(vehicle), ReadSingle(vehicle, "attack"), ReadSingle(vehicle, "sleaze"),
                ReadSingle(vehicle, "dataprocessing"), ReadSingle(vehicle, "firewall"),
                ReadSingle(vehicle, "attributearray"), ReadBoolean(vehicle, "canswapattributes"),
                out CharacterVehicleMatrixSwapState state))
        {
            throw new InvalidOperationException(
                "The selected Vehicle root is not an exact enabled CanSwapAttributes Matrix target.");
        }
        return state;
    }

    internal static XElement FindVehicle(XElement root, Guid vehicleId)
    {
        XElement container = root.Elements("vehicles").Single();
        XElement[] matches = container.Elements("vehicle")
            .Where(candidate => Guid.TryParse(ReadSingle(candidate, "guid"), out Guid id) && id == vehicleId)
            .Take(2).ToArray();
        return matches.Length == 1 ? matches[0]
            : throw new InvalidOperationException("Vehicle Guid identity is missing or ambiguous.");
    }

    private static string ReadDisplayName(XElement vehicle)
    {
        string name = ReadSingle(vehicle, "name");
        return string.IsNullOrEmpty(name) ? "Vehicle" : name;
    }

    private static string ReadSingle(XElement parent, string name)
    {
        XElement[] matches = parent.Elements(name).Take(2).ToArray();
        return matches.Length == 1 ? matches[0].Value
            : throw new InvalidOperationException($"Vehicle requires exactly one <{name}> element.");
    }

    private static bool ReadBoolean(XElement parent, string name)
        => bool.TryParse(ReadSingle(parent, name), out bool value) ? value
            : throw new InvalidOperationException($"Vehicle <{name}> must be a saved Boolean.");
}
