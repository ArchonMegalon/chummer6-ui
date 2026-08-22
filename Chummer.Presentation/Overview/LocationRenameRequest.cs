using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record LocationRenameRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    WorkspaceLocationKind Kind,
    Guid LocationId,
    string Name)
{
    public const int MaximumNameLength = 32767;

    internal static string ValidateName(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException("Location name cannot be empty.");
        }
        if (value.Length > MaximumNameLength)
        {
            throw new InvalidOperationException(
                $"Location name cannot exceed {MaximumNameLength} characters.");
        }
        return value;
    }
}
