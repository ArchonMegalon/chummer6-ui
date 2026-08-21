using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record VehicleLocationAddRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid? VehicleId,
    string Name)
{
    public const int MaximumNameLength = 32767;

    internal static string ValidateName(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException("Vehicle location name cannot be empty.");
        }
        if (value.Length > MaximumNameLength)
        {
            throw new InvalidOperationException(
                $"Vehicle location name cannot exceed {MaximumNameLength} characters.");
        }
        return value;
    }
}
