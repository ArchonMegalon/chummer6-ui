using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record WeaponLocationAddRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    string Name)
{
    public const int MaximumNameLength = 32767;

    internal static string ValidateName(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException("Weapon location name cannot be empty.");
        }
        if (value.Length > MaximumNameLength)
        {
            throw new InvalidOperationException(
                $"Weapon location name cannot exceed {MaximumNameLength} characters.");
        }
        return value;
    }
}
